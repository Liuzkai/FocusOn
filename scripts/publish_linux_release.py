"""Append verified Linux assets to an existing release without replacing Windows files."""
import hashlib
import json
import os
import pathlib
import re
import subprocess
import tempfile
import urllib.error
from github_api import api, error_summary

root = pathlib.Path(__file__).resolve().parent.parent
repo = os.environ['GH_REPO']
tag = os.environ.get('RELEASE_TAG', 'v0.1.0')
assert repo == 'Liuzkai/FocusOn', 'Unexpected publication repository'
assert re.fullmatch(r'v[0-9][A-Za-z0-9._-]*', tag), 'Invalid release tag'
source = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
report = {'sourceCommit': source, 'releaseTag': tag, 'runUrl': os.environ.get('RUN_URL'), 'status': 'failed'}
report_path = root / 'artifacts/linux-release-result.json'
report_path.parent.mkdir(exist_ok=True)

def snapshot(assets):
    return {a['name']: {'id': a['id'], 'size': a['size'], 'digest': a.get('digest')} for a in assets if a['name'].startswith('FocusOn-win-')}

def find_release():
    # The tag endpoint may omit unpublished drafts. List releases with the
    # workflow's contents-write permission and retain the exact matching ID.
    for page in range(1, 11):
        releases = api(f'repos/{repo}/releases?per_page=100&page={page}')
        for candidate in releases:
            if candidate['tag_name'] == tag:
                return candidate
        if len(releases) < 100:
            report['visibleReleases'] = [{'tag': value['tag_name'], 'draft': value['draft']} for value in releases]
            break
    raise RuntimeError(f'No visible release for {tag}; the Windows release was not modified.')

try:
    if os.environ.get('PACKAGE_RESULT', 'success') != 'success':
        raise RuntimeError('Linux build/package verification did not succeed; no assets were published.')
    release = find_release()
    release_id = release['id']
    windows = snapshot(release['assets'])
    if not windows:
        raise RuntimeError('The selected release has no Windows assets; refusing to publish to a different release.')
    assets = [root / 'artifacts/release' / (f'FocusOn-{rid}.tar.gz' + suffix)
              for rid in ['linux-x64', 'linux-arm64'] for suffix in ['', '.sha256']]
    for rid in ['linux-x64', 'linux-arm64']:
        subprocess.run(['python3', str(root / 'scripts/verify_delivery.py'), rid], cwd=root, check=True)
    existing = {asset['name']: asset for asset in release['assets']}
    for asset in assets:
        digest = 'sha256:' + hashlib.sha256(asset.read_bytes()).hexdigest()
        if asset.name in existing:
            if existing[asset.name].get('digest') != digest:
                raise RuntimeError(f'{asset.name} already exists with different contents; it was not replaced.')
        else:
            subprocess.run(['gh', 'release', 'upload', tag, str(asset), '--repo', repo], check=True)
    release = api(f'repos/{repo}/releases/{release_id}')
    assert snapshot(release['assets']) == windows, 'Windows asset metadata changed'
    with tempfile.TemporaryDirectory() as temporary:
        for asset in assets:
            subprocess.run(['gh', 'release', 'download', tag, '--repo', repo, '--pattern', asset.name, '--dir', temporary], check=True)
            downloaded = pathlib.Path(temporary) / asset.name
            assert hashlib.sha256(downloaded.read_bytes()).digest() == hashlib.sha256(asset.read_bytes()).digest(), f'Uploaded asset differs: {asset.name}'
    section = f'''<!-- focuson-linux -->
## Linux packages

- **x64:** `FocusOn-linux-x64.tar.gz`
- **ARM64:** `FocusOn-linux-arm64.tar.gz`
- Each archive includes the .NET runtime, a file manifest, platform instructions, and the GNOME Wayland extension. The adjacent `.sha256` file verifies the archive.
- Extract the complete folder and run `./FocusOn` in a graphical desktop session. X11 capture requires logind; GNOME Wayland requires enabling the bundled extension. Other Wayland desktops support history and title timers, but foreground capture is unavailable.
- English and Simplified Chinese can be selected in the top row. The language preference is saved locally; user titles and historical data remain unchanged.

**Source:** Linux assets were built from [{source}](https://github.com/{repo}/tree/{source}), the cross-platform implementation. The existing Windows `v0.1.0` tag and Windows assets are unchanged; GitHub's tag source archives therefore refer to the older Windows source. The Linux source commit is also recorded in each `manifest.json`.

**Validation:** core/platform and headless UI regressions, native virtual X11 capture and cross-process activation, and independent archive/file/SHA256/ELF/permission/runtime checks. ARM64 is cross-compiled; real ARM64 startup, GNOME Wayland, logind lock/sleep/display behavior, and hardware acceptance remain unverified.
<!-- /focuson-linux -->'''
    body = release.get('body') or ''
    if '<!-- focuson-linux -->' in body:
        body = re.sub(r'<!-- focuson-linux -->.*?<!-- /focuson-linux -->', lambda _: section, body, flags=re.S)
    else:
        body = body.rstrip() + '\n\n' + section
    api(f'repos/{repo}/releases/{release["id"]}', 'PATCH', {'body': body, 'draft': False})
    published = api(f'repos/{repo}/releases/{release_id}')
    assert not published['draft'] and snapshot(published['assets']) == windows
    selected = {asset.name for asset in assets}
    report.update(status='published', releaseUrl=published['html_url'], releaseId=published['id'], windowsAssets=windows,
        assets=[{'name': asset['name'], 'size': asset['size'], 'digest': asset.get('digest'), 'url': asset['browser_download_url']}
                for asset in published['assets'] if asset['name'] in selected])
    assert len(report['assets']) == 4
    print('Verified published Linux release: ' + published['html_url'])
except Exception as error:
    report['error'] = error_summary(error)
    raise
finally:
    report_path.write_text(json.dumps(report, indent=2) + '\n')
