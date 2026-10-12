"""Sign and notarize on a native Mac runner. Secret values are never logged."""
import base64
import json
import os
import pathlib
import plistlib
import secrets
import subprocess
import sys
import tempfile

app = pathlib.Path(sys.argv[1]).resolve()
root = pathlib.Path(__file__).resolve().parent.parent
assert sys.platform == 'darwin' and app.name == 'FocusOn.app'
report = app.parent / 'mac-signing.json'

def run(label, args):
    try:
        result = subprocess.run(args, capture_output=True, text=True, timeout=2700)
    except subprocess.TimeoutExpired:
        raise RuntimeError(label + ' timed out; sensitive command arguments withheld.') from None
    if result.returncode:
        if os.environ.get('FOCUSON_MAC_SIGN') != 'developer-id':
            # Ad-hoc commands contain no certificate/private-key material.
            print(result.stderr.strip(), file=sys.stderr)
        raise RuntimeError(f'{label} failed (exit {result.returncode}); sensitive tool output withheld.')
    return result.stdout

def code_files():
    for path in sorted(app.rglob('*')):
        if path.is_file() and not path.is_symlink():
            # Sign the main executable through the app last, after its nested code.
            if path == app / 'Contents/MacOS/FocusOn': continue
            with path.open('rb') as stream:
                if stream.read(4) in {b'\xcf\xfa\xed\xfe', b'\xfe\xed\xfa\xcf', b'\xca\xfe\xba\xbe', b'\xca\xfe\xba\xbf'}:
                    yield path

if os.environ.get('FOCUSON_MAC_SIGN') != 'developer-id':
    for path in code_files(): run('Ad-hoc nested signature', ['codesign', '--force', '--sign', '-', str(path)])
    run('Ad-hoc app signature', ['codesign', '--force', '--sign', '-', str(app)])
    run('Ad-hoc verification', ['codesign', '--verify', '--deep', '--strict', str(app)])
    configured = all(os.environ.get(name) for name in ['MACOS_CERTIFICATE_P12_BASE64', 'MACOS_CERTIFICATE_PASSWORD', 'MACOS_SIGNING_IDENTITY', 'APPLE_TEAM_ID', 'APPLE_API_KEY_ID', 'APPLE_API_ISSUER_ID', 'APPLE_API_PRIVATE_KEY'])
    report.write_text(json.dumps({'status': 'ad-hoc-validation', 'credentialsConfigured': configured, 'notarized': False, 'stapled': False, 'formalReady': False}, indent=2) + '\n')
    print('Mac validation bundle: ad-hoc only; no Developer ID or notarization claim.')
    sys.exit(0)

required = ['MACOS_CERTIFICATE_P12_BASE64', 'MACOS_CERTIFICATE_PASSWORD', 'MACOS_SIGNING_IDENTITY', 'APPLE_TEAM_ID', 'APPLE_API_KEY_ID', 'APPLE_API_ISSUER_ID', 'APPLE_API_PRIVATE_KEY']
missing = [name for name in required if not os.environ.get(name)]
if missing: raise SystemExit('Missing Actions Secrets: ' + ', '.join(missing))
identity, team = os.environ['MACOS_SIGNING_IDENTITY'], os.environ['APPLE_TEAM_ID']
assert identity.startswith('Developer ID Application: ') and identity.endswith('(' + team + ')'), 'Developer ID Application identity/team mismatch'
entitlements = root / 'platforms/macos/entitlements.plist'
assert set(plistlib.loads(entitlements.read_bytes())) == {'com.apple.security.cs.allow-jit', 'com.apple.security.cs.allow-unsigned-executable-memory'}
original_keychains = subprocess.check_output(['security', 'list-keychains', '-d', 'user'], text=True)
import shlex
original_keychains = shlex.split(original_keychains)
with tempfile.TemporaryDirectory(prefix='focuson-sign-', dir=os.environ.get('RUNNER_TEMP')) as temporary:
    temporary = pathlib.Path(temporary)
    certificate = temporary / 'developer-id.p12'; certificate.write_bytes(base64.b64decode(os.environ['MACOS_CERTIFICATE_P12_BASE64'], validate=True)); certificate.chmod(0o600)
    key = temporary / 'AuthKey.p8'; key.write_text(os.environ['APPLE_API_PRIVATE_KEY']); key.chmod(0o600)
    keychain = str(temporary / 'signing.keychain-db'); password = secrets.token_urlsafe(32)
    created = False
    try:
        run('Create temporary keychain', ['security', 'create-keychain', '-p', password, keychain]); created = True
        run('Unlock temporary keychain', ['security', 'unlock-keychain', '-p', password, keychain])
        run('Keychain timeout', ['security', 'set-keychain-settings', '-lut', '21600', keychain])
        run('Import certificate', ['security', 'import', str(certificate), '-k', keychain, '-P', os.environ['MACOS_CERTIFICATE_PASSWORD'], '-T', '/usr/bin/codesign'])
        run('Key partitions', ['security', 'set-key-partition-list', '-S', 'apple-tool:,apple:', '-s', '-k', password, keychain])
        run('Keychain search list', ['security', 'list-keychains', '-d', 'user', '-s', keychain, *original_keychains])
        identities = run('Validate unexpired identity', ['security', 'find-identity', '-v', '-p', 'codesigning', keychain])
        assert identity in identities and '0 valid identities' not in identities, 'No valid requested Developer ID certificate in supplied P12'
        for path in code_files():
            arguments = ['codesign', '--force', '--sign', identity, '--keychain', keychain, '--timestamp']
            arguments += ['--options', 'runtime']
            run('Sign nested code', [*arguments, str(path)])
        run('Sign app', ['codesign', '--force', '--sign', identity, '--keychain', keychain, '--timestamp', '--options', 'runtime', '--entitlements', str(entitlements), str(app)])
        run('Verify signatures', ['codesign', '--verify', '--deep', '--strict', '--verbose=2', str(app)])
        # Verify actual certificate authority, team, hardened runtime and the exact entitlement set.
        details = subprocess.run(['codesign', '-dv', '--verbose=4', str(app)], capture_output=True, text=True, check=True).stderr
        assert 'Authority=Developer ID Application:' in details and 'TeamIdentifier=' + team in details and 'runtime' in details
        actual = run('Read entitlements', ['codesign', '-d', '--entitlements', ':-', str(app)])
        assert plistlib.loads(actual.encode()) == plistlib.loads(entitlements.read_bytes())
        submission = temporary / 'notarize.zip'
        run('Notary archive', ['ditto', '-c', '-k', '--sequesterRsrc', '--keepParent', str(app), str(submission)])
        result = json.loads(run('Apple notarization', ['xcrun', 'notarytool', 'submit', str(submission), '--key', str(key), '--key-id', os.environ['APPLE_API_KEY_ID'], '--issuer', os.environ['APPLE_API_ISSUER_ID'], '--wait', '--output-format', 'json']))
        assert result.get('status') == 'Accepted', 'Apple did not accept notarization; inspect the submission in the Apple account'
        run('Staple app', ['xcrun', 'stapler', 'staple', str(app)])
        run('Validate staple', ['xcrun', 'stapler', 'validate', str(app)])
        run('Verify stapled signatures', ['codesign', '--verify', '--deep', '--strict', str(app)])
        run('Gatekeeper assessment', ['spctl', '--assess', '--type', 'execute', '--verbose=4', str(app)])
        report.write_text(json.dumps({'status': 'developer-id-notarized', 'team': team, 'submissionId': result['id'], 'notarized': True, 'stapled': True,
            'hardenedRuntime': True, 'entitlements': list(plistlib.loads(entitlements.read_bytes())), 'formalReady': False,
            'pending': 'Physical desktop, foreground permission and power acceptance'}, indent=2) + '\n')
        print('Developer ID, hardened runtime, Apple acceptance, staple and Gatekeeper checks passed; device acceptance remains pending.')
    finally:
        if created:
            subprocess.run(['security', 'list-keychains', '-d', 'user', '-s', *original_keychains], capture_output=True)
            subprocess.run(['security', 'delete-keychain', keychain], capture_output=True)
