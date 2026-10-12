#!/usr/bin/env python3
"""Register this extracted portable package in the current user's application menu."""
import os
import pathlib
import shutil
import subprocess

package = pathlib.Path(__file__).resolve().parent
executable = package / 'FocusOn'
if not executable.is_file(): raise SystemExit('Keep this installer next to the extracted FocusOn executable.')
if any(ord(c) < 32 for c in str(package)): raise SystemExit('Installation path cannot contain control characters.')
configured = os.environ.get('XDG_DATA_HOME', '')
data = pathlib.Path(configured) if configured.startswith('/') else pathlib.Path.home() / '.local/share'
applications = data / 'applications'; applications.mkdir(parents=True, exist_ok=True)
icons = data / 'icons/hicolor'
shutil.copytree(package / 'icons/hicolor', icons, dirs_exist_ok=True)
escaped = str(executable).replace('\\', '\\\\\\\\').replace('"', '\\\\"').replace('`', '\\\\`').replace('$', '\\\\$').replace('%', '%%')
entry = (package / 'focuson.desktop').read_text().replace('Exec=FocusOn\n', 'Exec="' + escaped + '"\n')
target = applications / 'focuson.desktop'; target.write_text(entry)
if shutil.which('update-desktop-database'): subprocess.run(['update-desktop-database', str(applications)], check=True)
print('Registered ' + str(target) + '. Keep the package in this location; rerun after moving it.')
