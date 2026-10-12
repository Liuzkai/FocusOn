"""Publish an explicitly approved, already verified draft without rebuilding assets."""
import hashlib
import json
import os
import pathlib
import re
import subprocess
import tarfile
import tempfile
import zipfile
from github_api import api, error_summary

root = pathlib.Path(__file__).resolve().parent.parent
repo, tag = os.environ['GH_REPO'], os.environ['RELEASE_TAG']
assert repo == 'Liuzkai/FocusOn' and tag != 'v0.1.0'
assert re.fullmatch(r'v\d+\.\d+\.\d+', tag), 'A stable existing version is required'
approval = json.loads((root / '.github/release-publication' / (tag + '.json')).read_text())
assert approval['tag'] == tag and approval['publishExistingDraft'] is True
assert approval['authorization'].strip(), 'Explicit user approval must be recorded'
source = approval['sourceCommit']
assert re.fullmatch(r'[0-9a-f]{40}', source)
result = {'sourceCommit': source, 'releaseTag': tag, 'status': 'failed',
          'buildRunUrl': f'https://github.com/{repo}/actions/runs/{approval["buildRunId"]}'}
result_file = root / 'artifacts/desktop-release-result.json'
result_file.parent.mkdir(parents=True, exist_ok=True)

def tag_commit(name):
    reference = api(f'repos/{repo}/git/ref/tags/{name}')['object']
    while reference['type'] == 'tag':
        reference = api(f'repos/{repo}/git/tags/{reference["sha"]}')['object']
    assert reference['type'] == 'commit'
    return reference['sha']

def find(name):
    for page in range(1, 11):
        releases = api(f'repos/{repo}/releases?per_page=100&page={page}')
        for release in releases:
            if release['tag_name'] == name: return release
        if len(releases) < 100: break
    raise AssertionError('Existing release missing: ' + name)

def snapshot(release):
    return {a['name']: {k: a.get(k) for k in ['id', 'size', 'digest']} for a in release['assets']}

try:
    assert tag_commit(tag) == source, 'Immutable tag no longer matches the approved package source'
    historical = find('v0.1.0')
    historical_assets, historical_tag = snapshot(historical), tag_commit('v0.1.0')
    run = api(f'repos/{repo}/actions/runs/{approval["buildRunId"]}')
    assert run['head_sha'] == source and run['status'] == 'completed' and run['conclusion'] == 'success', 'Native release validation did not succeed at this source'
    jobs = api(f'repos/{repo}/actions/runs/{approval["buildRunId"]}/jobs?per_page=100')['jobs']
    expected_jobs = {'build / Native ' + rid for rid in ['win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64']}
    assert expected_jobs <= {j['name'] for j in jobs if j['status'] == 'completed' and j['conclusion'] == 'success'}, 'Missing successful native targets'
    release = find(tag)
    assert release['target_commitish'] == source, 'Draft source differs from the approved immutable tag'
    expected = {a['name']: a for a in approval['assets']}
    names = {f'FocusOn-{rid}{extension}{checksum}' for rid, extension in [('win-x64', '.zip'), ('win-arm64', '.zip'), ('linux-x64', '.tar.gz'), ('linux-arm64', '.tar.gz')] for checksum in ['', '.sha256']}
    assert set(expected) == names == {a['name'] for a in release['assets']}, 'Unexpected or missing release attachments'
    before = snapshot(release)
    for asset in release['assets']:
        assert all(asset[k] == expected[asset['name']][k] for k in ['size', 'digest']), 'Existing attachment differs from the verified draft'
    with tempfile.TemporaryDirectory(prefix='focuson-publication-check-') as temporary:
        subprocess.run(['gh', 'release', 'download', tag, '--repo', repo, '--dir', temporary], check=True)
        for name, asset in expected.items():
            path = pathlib.Path(temporary) / name
            with path.open('rb') as stream: digest = 'sha256:' + hashlib.file_digest(stream, 'sha256').hexdigest()
            assert path.stat().st_size == asset['size'] and digest == asset['digest'], 'Downloaded attachment changed: ' + name
            if name.endswith('.sha256'): continue
            rid = name.removeprefix('FocusOn-').removesuffix('.tar.gz').removesuffix('.zip')
            manifest_name = 'FocusOn-' + rid + '/manifest.json'
            if path.suffix == '.zip':
                with zipfile.ZipFile(path) as archive: manifest = json.loads(archive.read(manifest_name))
            else:
                with tarfile.open(path) as archive: manifest = json.load(archive.extractfile(manifest_name))
            assert manifest['sourceCommit'] == source and manifest['runtime'] == rid and manifest['buildKind'] == 'release'
            checksum, filename = pathlib.Path(str(path) + '.sha256').read_text().split()
            assert checksum.lower() == digest.removeprefix('sha256:') and filename == name
    # Re-read after download checks to catch a concurrent attachment change.
    assert snapshot(api(f'repos/{repo}/releases/{release["id"]}')) == before
    assert tag_commit(tag) == source
    if release['draft']:
        body = f'''Windows and Linux x64 / ARM64 packages from `{source}`, matching immutable tag `{tag}`.

Includes the FocusOn executable/window/tray icons, close-to-tray with restore and explicit Exit, recoverable Linux tray fallback, English / Simplified Chinese with a saved language preference, and the .NET runtime. Existing local records remain compatible. Archives retain MIT and third-party notices; adjacent SHA256 files verify downloads.

Validation: six standard native GitHub-hosted targets passed their core/platform and UI regressions, native desktop lifecycle checks, and self-contained file/archive/architecture/runtime verification. Mac native foreground recording and actual .app startup / Quit passed. Linux checks include X11 capture and isolated StatusNotifier registration / host loss. Physical power, DPI, desktop tray panels and permission acceptance remain pending.

macOS is not included in this release. Apple credentials are not configured; Mac outputs are ad-hoc validation artifacts, not Developer ID-signed or notarized distributions. Signing, hardened runtime, notarization, stapling and Gatekeeper verification are configured but have not completed. See the repository's docs/macos.md for secure Secrets and permission setup.

Build: {result['buildRunUrl']}
The existing v0.1.0 tag and all Windows/Linux attachments are preserved, including their documented historical source differences.
'''
        api(f'repos/{repo}/releases/{release["id"]}', 'PATCH', {'draft': False, 'prerelease': False, 'make_latest': 'true', 'body': body})
    published = api(f'repos/{repo}/releases/{release["id"]}')
    assert not published['draft'] and not published['prerelease'] and published['published_at']
    assert published['tag_name'] == tag and published['target_commitish'] == source and snapshot(published) == before
    assert snapshot(api(f'repos/{repo}/releases/{historical["id"]}')) == historical_assets and tag_commit('v0.1.0') == historical_tag
    result.update(status='published', releaseUrl=published['html_url'], publishedAt=published['published_at'],
                  assets=[{k: a[k] for k in ['name', 'size', 'digest', 'browser_download_url']} for a in published['assets']])
    print('Published and verified: ' + published['html_url'])
except Exception as error:
    result['error'] = error_summary(error)
    raise
finally:
    result_file.write_text(json.dumps(result, indent=2) + '\n')
