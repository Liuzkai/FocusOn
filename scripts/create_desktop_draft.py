"""Create a draft only after native validation; never clobber any release asset."""
import hashlib
import json
import os
import pathlib
import re
import stat
import subprocess
import tarfile
import tempfile
import zipfile
from github_api import api, error_summary

root = pathlib.Path(__file__).resolve().parent.parent
repo, tag = os.environ['GH_REPO'], os.environ['RELEASE_TAG']
assert repo == 'Liuzkai/FocusOn' and tag != 'v0.1.0'
assert re.fullmatch(r'v\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?', tag)
source = subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip()
reference = api(f'repos/{repo}/git/ref/tags/{tag}')['object']
while reference['type'] == 'tag': reference = api(f'repos/{repo}/git/tags/{reference["sha"]}')['object']
assert reference['type'] == 'commit' and reference['sha'] == source, 'Tag must point to every package source'
report = {'sourceCommit':source,'releaseTag':tag,'status':'failed','macExcluded':[]}
result_path = root / 'artifacts/desktop-release-result.json'

def releases():
    for page in range(1, 11):
        batch = api(f'repos/{repo}/releases?per_page=100&page={page}')
        yield from batch
        if len(batch) < 100: return
    raise RuntimeError('Release pagination limit reached')

def find(tag_name):
    return next((release for release in releases() if release['tag_name'] == tag_name), None)

def snapshot(release):
    return {**{k:release.get(k) for k in ['tag_name','target_commitish','name','body','draft','prerelease']},
            'assets':{a['name']:{k:a.get(k) for k in ['id','size','digest']} for a in release['assets']}}

def extract(archive, package):
    with (zipfile.ZipFile(archive) if archive.suffix == '.zip' else tarfile.open(archive)) as stream:
        if archive.suffix == '.zip':
            for member in stream.infolist():
                path = pathlib.PurePosixPath(member.filename)
                if package.name.startswith('FocusOn-osx-') and path.parts[0] == '__MACOSX':
                    assert '..' not in path.parts and not path.is_absolute()
                    continue
                assert path.parts[0] == package.name and '..' not in path.parts and not path.is_absolute()
                mode = member.external_attr >> 16
                assert not stat.S_ISLNK(mode), 'Unexpected ZIP symlink'
            stream.extractall(package.parent)
            for member in stream.infolist():
                mode = member.external_attr >> 16
                if mode: (package.parent / member.filename).chmod(stat.S_IMODE(mode))
        else: stream.extractall(package.parent, filter='data')

try:
    historical = [release for release in releases() if release['tag_name'] != tag]
    assert any(release['tag_name'] == 'v0.1.0' for release in historical), 'Original release is missing'
    before = {release['id']:snapshot(release) for release in historical}
    assets = []
    for rid in ['win-x64','win-arm64','linux-x64','linux-arm64','osx-x64','osx-arm64']:
        package = root / 'artifacts/release' / ('FocusOn-' + rid)
        archive = pathlib.Path(str(package) + ('.tar.gz' if rid.startswith('linux-') else '.zip'))
        extract(archive, package)
        subprocess.run(['python3','scripts/verify_delivery.py',rid],check=True)
        if rid.startswith('osx-'):
            signing = json.loads((package / 'mac-signing.json').read_text())
            if signing['status'] != 'developer-id-notarized' or not signing['notarized'] or not signing['stapled']:
                report['macExcluded'].append(rid); continue
        assets += [archive, pathlib.Path(str(archive) + '.sha256')]
        if rid.startswith('win-'):
            installer = root / 'artifacts/release' / f'FocusOn-{rid}-Setup.exe'
            evidence_files = list((root / 'artifacts/evidence').rglob('installer.json'))
            native = [json.loads(p.read_text(encoding='utf-8-sig')) for p in evidence_files if json.loads(p.read_text(encoding='utf-8-sig'))['runtime'] == rid]
            assert len(native) == 1 and native[0]['sourceCommit'] == source
            assert native[0]['nativeInstallation'].startswith('PASS:')
            assert installer.stat().st_size == native[0]['bytes']
            assert hashlib.sha256(installer.read_bytes()).hexdigest().upper() == native[0]['sha256']
            checksum = pathlib.Path(str(installer) + '.sha256')
            assert checksum.read_text().split() == [native[0]['sha256'], installer.name]
            assets += [installer, checksum]

    release = find(tag)
    if release is not None:
        assert release['draft'], 'Published releases are immutable; use a new version'
        assert release['target_commitish'] == source, 'Existing draft is from a different commit'
    else:
        body = f'''Native Windows and Linux builds from `{source}`, matching immutable tag `{tag}`.

Includes the .NET runtime, the integrated FocusOn icon, recoverable tray behavior, and English / Simplified Chinese with a saved language preference. Archives include MIT and third-party notices; adjacent SHA256 files verify downloads.

The time range selector offers 1h, 2h, 4h, 6h, 8h, 12h, 24h and Today; the default is 1h. Hour ranges are elapsed lookbacks and can cross midnight. Today shows local midnight to now. Timeline and intersecting logs follow the visible range; daily totals and deletion retain the date picker's daily scope. Historical dates use their day end as the reference. Zoom/pan stays fixed during refresh; Reset restores the selected preset and live following. Cross-day projections are joined in one recorder snapshot without duplicate records or duration rounding loss.

Settings now contains saved English/Chinese and Light/Dark choices, a login startup switch, a data-folder shortcut and explicit copy/verify/restart relocation with the original folder preserved. About shows the actual build version, native architecture and repository. Updates are checked only on request against public stable releases; Windows verifies package SHA256 before save/exit and per-user installation. Windows x64/ARM64 Setup.exe assets include shortcuts and uninstall/upgrade support, preserve records outside the program folder and have native installation/installed-app/upgrade/uninstall verification. These installers are unsigned; SHA256 is integrity checking, not publisher authentication. Other platforms use manual replacement from the matching official release.

Statistics has its own page with inclusive multi-day ranges (last 7 / 30 days, up to 366 dates) and a local single-day HH:mm period with 24:00 as the day end. Dark donut app shares, rounded duration bars, top-three app trends and a full app list use real foreground Usage only; timers, pauses and gaps are excluded. Original monotonic allocation endpoints preserve exact range/bucket totals, including deleted parts and DST repeated hours. Applied scope, empty/error/loading states, English/Chinese, high contrast, responsive charts, pointer details and keyboard navigation are supported. Switching pages preserves review selection/zoom and continues recording and timers.

Timeline hints follow the pointer and refresh without hover delays. Scrolling to the log end follows appended records; scrolling up or selecting history pauses following. Small separators mark contiguous different-app boundaries without changing recorded times or durations.

Daily app totals can be scrolled by dragging the app labels, using the wheel or horizontal trackpad, or using arrow/Home/End/Page keys. A three-DIP position line appears during interaction and hides after about one second idle, in a small footer that never covers labels. App colors are assigned for perceptual separation and saved across restarts. On the timeline, left-click selects a record, left-drag filters a range, the wheel zooms, right-drag pans, and right-click zooms to the selected range without clearing its filter. Escape or Clear range removes the range filter.

Validation: native standard GitHub-hosted x64/ARM64 runners, SQLite and UI regressions, native desktop residency/timer/exit checks, and file/archive/architecture/runtime verification. Linux includes isolated X11 capture and StatusNotifier registration/host-loss checks. Real desktop power, GNOME Wayland and foreground privacy permission acceptance remains pending.

macOS bundles are attached only after Developer ID signing, hardened runtime, Apple notarization, stapling and Gatekeeper verification succeed on native Intel / Apple Silicon runners. Missing credentials exclude Mac attachments. Even signed Mac bundles remain pending physical desktop and Accessibility permission acceptance. This release stays a draft.

Build: https://github.com/{repo}/actions/runs/{os.environ['RUN_ID']}
The existing v0.1.0 tag and all Windows/Linux attachments are preserved; their historical source difference remains documented there.
'''
        release = api(f'repos/{repo}/releases','POST',{'tag_name':tag,'target_commitish':source,'name':'FocusOn ' + tag,'body':body,'draft':True,'prerelease':'-' in tag})
    existing = {a['name']:a for a in release['assets']}
    for asset in assets:
        digest = 'sha256:' + hashlib.sha256(asset.read_bytes()).hexdigest()
        if asset.name in existing:
            assert existing[asset.name].get('digest') == digest, f'{asset.name} already exists with different bytes; not replaced'
        else: subprocess.run(['gh','release','upload',tag,str(asset),'--repo',repo],check=True)
    updated = api(f'repos/{repo}/releases/{release["id"]}'); assert updated['draft']
    with tempfile.TemporaryDirectory(prefix='focuson-download-check-') as temporary:
        for asset in assets:
            subprocess.run(['gh','release','download',tag,'--repo',repo,'--pattern',asset.name,'--dir',temporary],check=True)
            assert hashlib.sha256((pathlib.Path(temporary) / asset.name).read_bytes()).digest() == hashlib.sha256(asset.read_bytes()).digest()
    for release_id, original in before.items():
        assert snapshot(api(f'repos/{repo}/releases/{release_id}')) == original, 'Historical release changed'
    report['preservedReleases'] = list(before.values())
    report.update(status='draft',releaseUrl=updated['html_url'],assets=[{'name':a['name'],'size':a['size'],'digest':a.get('digest'),'url':a['browser_download_url']} for a in updated['assets']])
    print('Verified draft: ' + updated['html_url'])
except Exception as error:
    report['error'] = error_summary(error); raise
finally:
    result_path.write_text(json.dumps(report,indent=2) + '\n')
