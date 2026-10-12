import hashlib
import json
import pathlib
import struct
import subprocess
import zipfile
import sys

root = pathlib.Path(__file__).resolve().parent.parent
package_name = sys.argv[1] if len(sys.argv) > 1 else 'FocusOn-win-x64'
package = root / 'artifacts' / package_name
archive = root / 'artifacts' / (package_name + '.zip')
manifest = json.loads((package / 'manifest.json').read_text(encoding='utf-8-sig'))
source = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
assert manifest['sourceCommit'] == source, 'Manifest source differs from HEAD'
assert manifest['runtime'] == 'win-x64' and manifest['selfContained'] is True
expected = {entry['path'].replace('\\', '/'): entry for entry in manifest['files']}
actual = {p.relative_to(package).as_posix() for p in package.rglob('*') if p.is_file()}
assert actual == set(expected) | {'manifest.json'}, 'Unexpected or missing package files'
for name, entry in expected.items():
    data = (package / name).read_bytes()
    assert len(data) == entry['bytes'], f'Size mismatch: {name}'
    assert hashlib.sha256(data).hexdigest().upper() == entry['sha256'].upper(), f'Hash mismatch: {name}'
with zipfile.ZipFile(archive) as z:
    files = {i.filename for i in z.infolist() if not i.is_dir()}
    assert files == {package.name + '/' + name for name in actual}, 'ZIP file inventory mismatch'
    assert z.testzip() is None, 'ZIP CRC failure'
    for name in actual:
        assert z.read(package.name + '/' + name) == (package / name).read_bytes(), f'ZIP mismatch: {name}'
architecture = {}
for name in ['FocusOn.exe', 'coreclr.dll', 'e_sqlite3.dll']:
    data = (package / name).read_bytes()
    assert data[:2] == b'MZ', f'Invalid executable: {name}'
    offset = struct.unpack_from('<I', data, 0x3c)[0]
    assert data[offset:offset + 4] == b'PE\0\0'
    machine = struct.unpack_from('<H', data, offset + 4)[0]
    assert machine == 0x8664, f'Not AMD64: {name}'
    architecture[name] = 'AMD64'
zip_hash = hashlib.sha256(archive.read_bytes()).hexdigest().upper()
declared_hash = pathlib.Path(str(archive) + '.sha256').read_text().split()[0]
assert zip_hash == declared_hash, 'ZIP SHA256 mismatch'
config = json.loads((package / 'FocusOn.runtimeconfig.json').read_text())
frameworks = config['runtimeOptions']['includedFrameworks']
assert {f['name'] for f in frameworks} >= {'Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App'}
assert not config['runtimeOptions'].get('framework'), 'Framework-dependent package'
result = dict(sourceCommit=source, verifiedFiles=len(expected), zipBytes=archive.stat().st_size,
              zipSha256=zip_hash, architecture=architecture, includedFrameworks=frameworks,
              manifestAndZip='passed', scope='file integrity only; no Windows UI or offline execution')
(root / 'artifacts' / (package_name + '-verification.json')).write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps(result, indent=2))
