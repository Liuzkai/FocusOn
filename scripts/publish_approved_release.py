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

def all_releases():
    for page in range(1, 11):
        batch = api(f'repos/{repo}/releases?per_page=100&page={page}')
        yield from batch
        if len(batch) < 100: return
    raise RuntimeError('Release pagination limit reached')

def historical_snapshot(release):
    return {**{k: release.get(k) for k in ['tag_name', 'target_commitish', 'name', 'body', 'draft', 'prerelease']},
            'assets': snapshot(release), 'tagObject': api(f'repos/{repo}/git/ref/tags/{release["tag_name"]}')['object']}

def check_history(older):
    for release_id, before_release in older.items():
        assert historical_snapshot(api(f'repos/{repo}/releases/{release_id}')) == before_release, 'An older release or tag changed'

try:
    assert tag_commit(tag) == source, 'Immutable tag no longer matches the approved package source'
    historical = find('v0.1.0')
    historical_assets, historical_tag = snapshot(historical), tag_commit('v0.1.0')
    older = {release['id']: historical_snapshot(release) for release in all_releases() if release['tag_name'] != tag}
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
    check_history(older)
    if release['draft']:
        body = (release.get('body') or '').replace('This release stays a draft.',
            'Published on explicit user approval after verification of the existing attachments.')
        body += f'''\n\n## Publication\n\nPublished the existing Windows/Linux x64 and ARM64 archives without rebuilding or replacing attachments. Source `{source}` matches immutable tag `{tag}`. Eight downloads, SHA256 files, manifests and the successful six-target native build were rechecked before publication. Daily records remain compatible; archives retain MIT and third-party notices.\n\nmacOS is excluded: current bundles are ad-hoc validation only, without Developer ID signing, notarization or stapling. Physical desktop permissions, power and DPI acceptance remains pending. See docs/macos.md for securely configuring Apple Secrets.\n\nVerified build: {result['buildRunUrl']}\nAll earlier releases and tags are preserved.\n'''
        api(f'repos/{repo}/releases/{release["id"]}', 'PATCH', {'draft': False, 'prerelease': False, 'make_latest': 'true', 'body': body})
    published = api(f'repos/{repo}/releases/{release["id"]}')
    assert not published['draft'] and not published['prerelease'] and published['published_at']
    assert published['tag_name'] == tag and published['target_commitish'] == source and snapshot(published) == before
    assert snapshot(api(f'repos/{repo}/releases/{historical["id"]}')) == historical_assets and tag_commit('v0.1.0') == historical_tag
    check_history(older)
    result.update(status='published', releaseUrl=published['html_url'], publishedAt=published['published_at'],
                  assets=[{k: a[k] for k in ['name', 'size', 'digest', 'browser_download_url']} for a in published['assets']],
                  preservedReleases=list(older.values()))
    print('Published and verified: ' + published['html_url'])
except Exception as error:
    result['error'] = error_summary(error)
    raise
finally:
    result_file.write_text(json.dumps(result, indent=2) + '\n')
