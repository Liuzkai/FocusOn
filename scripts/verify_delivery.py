"""Independently verify a self-contained package produced from the current commit."""
import hashlib
import json
import pathlib
import plistlib
import struct
import subprocess
import sys
import tarfile
import zipfile

root = pathlib.Path(__file__).resolve().parent.parent
runtime = (sys.argv[1] if len(sys.argv) > 1 else 'win-x64').removeprefix('FocusOn-')
assert runtime in {'win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64'}
package_parent = pathlib.Path(sys.argv[2]).resolve() if len(sys.argv) > 2 else root / 'artifacts' / 'release'
package = package_parent / ('FocusOn-' + runtime)
archive = pathlib.Path(str(package) + ('.zip' if runtime.startswith('win-') else '.tar.gz'))
source = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
manifest = json.loads((package / 'manifest.json').read_text(encoding='utf-8-sig'))
assert manifest['sourceCommit'] == source and manifest['baseCommit'] == source
assert manifest['buildKind'] == 'release' and manifest['runtime'] == runtime and manifest['selfContained'] is True

def digest(data):
    return hashlib.sha256(data).hexdigest().upper()

expected = {entry['path']: (entry['bytes'], entry['sha256'].upper()) for entry in manifest['files']}
assert len(expected) == len(manifest['files']), 'Duplicate manifest entries'
manifest_data = (package / 'manifest.json').read_bytes()
expected['manifest.json'] = (len(manifest_data), digest(manifest_data))
actual = {p.relative_to(package).as_posix() for p in package.rglob('*') if p.is_file()}
assert actual == set(expected), 'Unexpected or missing files'
assert {'README.md', 'LICENSE', 'licenses/README.md', 'licenses/Avalonia-MIT.txt', 'licenses/Inter-OFL.txt'} <= actual, 'Missing documentation or license notices'
for name, (size, sha) in expected.items():
    data = (package / name).read_bytes()
    assert len(data) == size and digest(data) == sha, f'File mismatch: {name}'

archived = {}
if runtime.startswith('win-'):
    with zipfile.ZipFile(archive) as stream:
        assert stream.testzip() is None
        for entry in stream.infolist():
            if not entry.is_dir():
                name = entry.filename.split('/', 1)[1]
                assert name not in archived, 'Duplicate archive member'
                data = stream.read(entry)
                archived[name] = (len(data), digest(data))
else:
    with tarfile.open(archive) as stream:
        for entry in stream:
            if entry.isfile():
                name = entry.name.split('/', 1)[1]
                assert name not in archived, 'Duplicate archive member'
                data = stream.extractfile(entry).read()
                archived[name] = (len(data), digest(data))
                if name == 'FocusOn' or name.endswith('/MacOS/FocusOn'):
                    assert entry.mode & 0o111, 'Archive lost executable permission'
assert archived == expected, 'Archive contents differ from the verified directory'

def architectures(data):
    if data[:2] == b'MZ':
        offset = struct.unpack_from('<I', data, 0x3c)[0]
        assert data[offset:offset + 4] == b'PE\0\0'
        return {struct.unpack_from('<H', data, offset + 4)[0]}
    if data[:4] == b'\x7fELF':
        assert data[4] == 2
        return {struct.unpack_from('<H' if data[5] == 1 else '>H', data, 18)[0]}
    if data[:4] in (b'\xcf\xfa\xed\xfe', b'\xfe\xed\xfa\xcf'):
        return {struct.unpack_from('<I' if data[0] == 0xcf else '>I', data, 4)[0]}
    if data[:4] in (b'\xca\xfe\xba\xbe', b'\xca\xfe\xba\xbf'):
        count = struct.unpack_from('>I', data, 4)[0]
        size = 32 if data[3] == 0xbf else 20
        return {struct.unpack_from('>I', data, 8 + i * size)[0] for i in range(count)}
    raise AssertionError('Unknown executable format')

windows, mac = runtime.startswith('win-'), runtime.startswith('osx-')
native = package / 'FocusOn.app/Contents/MacOS' if mac else package
target = (0xaa64 if 'arm64' in runtime else 0x8664) if windows else (0x100000c if 'arm64' in runtime else 0x1000007) if mac else (183 if 'arm64' in runtime else 62)
files = ['FocusOn.exe' if windows else 'FocusOn', 'coreclr.dll' if windows else 'libcoreclr.dylib' if mac else 'libcoreclr.so',
         'e_sqlite3.dll' if windows else 'libe_sqlite3.dylib' if mac else 'libe_sqlite3.so',
         'libSkiaSharp.dll' if windows else 'libSkiaSharp.dylib' if mac else 'libSkiaSharp.so']
if mac:
    files.append('libAvaloniaNative.dylib')
for name in files:
    assert target in architectures((native / name).read_bytes()), f'Wrong architecture: {name}'
if not windows:
    assert (native / 'FocusOn').stat().st_mode & 0o111
options = json.loads((native / 'FocusOn.runtimeconfig.json').read_text())['runtimeOptions']
assert any(f['name'] == 'Microsoft.NETCore.App' for f in options['includedFrameworks']) and not options.get('framework')
assert json.loads((native / 'FocusOn.deps.json').read_text())['runtimeTarget']['name'].endswith('/' + runtime)
if mac:
    info = plistlib.loads((package / 'FocusOn.app/Contents/Info.plist').read_bytes())
    assert info['CFBundleExecutable'] == 'FocusOn' and info['LSMinimumSystemVersion'] == '15.0'
    assert (package / 'FocusOn.app/Contents/Resources/focuson.icns').is_file()
sha = digest(archive.read_bytes())
assert pathlib.Path(str(archive) + '.sha256').read_text().split()[0].upper() == sha
report = {'runtime': runtime, 'sourceCommit': source, 'files': len(manifest['files']), 'archiveBytes': archive.stat().st_size,
          'archiveSha256': sha, 'nativeFilesChecked': files, 'result': 'PASS'}
(root / 'artifacts' / ('delivery-verification-' + runtime + '.json')).write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report))
