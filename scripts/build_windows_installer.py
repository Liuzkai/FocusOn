"""Build a per-user installer only from an already verified native release archive."""
import hashlib
import json
import os
import pathlib
import subprocess
import sys
import xml.etree.ElementTree as ET
root = pathlib.Path(__file__).resolve().parent.parent
rid = sys.argv[1]
assert rid in ('win-x64', 'win-arm64') and sys.platform == 'win32'
package = root / 'artifacts/release' / ('FocusOn-' + rid)
manifest = json.loads((package / 'manifest.json').read_text(encoding='utf-8-sig'))
source = subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()
assert manifest['sourceCommit'] == source and manifest['runtime'] == rid
version = ET.parse(root / 'Directory.Build.props').findtext('.//Version')
iscc = pathlib.Path(os.environ['FOCUSON_ISCC'])
assert iscc.is_file(), 'Inno Setup 6 is required'
subprocess.run([str(iscc), '/DSourceDir=' + str(package), '/DOutputDir=' + str(package.parent),
               '/DAppVersion=' + version, '/DRuntime=' + rid, '/DArchitecture=' + ('arm64' if rid == 'win-arm64' else 'x64compatible'),
               str(root / 'platforms/windows/FocusOn.iss')], check=True)
installer = package.parent / ('FocusOn-' + rid + '-Setup.exe')
sha = hashlib.sha256(installer.read_bytes()).hexdigest().upper()
installer.with_name(installer.name + '.sha256').write_text(sha + '  ' + installer.name + '\n')
report = {'runtime': rid, 'sourceCommit': source, 'version': version, 'name': installer.name,
          'bytes': installer.stat().st_size, 'sha256': sha, 'signed': False, 'nativeInstallation': 'pending', 'compiler': 'Inno Setup 6.5.4', 'compilerDownloadSha256': 'FA73BF47A4DA250D185D07561C2BFDA387E5E20DB77E4570004CF6A133CC10B1'}
evidence = root / 'artifacts/evidence' / rid
evidence.mkdir(parents=True, exist_ok=True)
(evidence / 'installer.json').write_text(json.dumps(report, indent=2) + '\n')
