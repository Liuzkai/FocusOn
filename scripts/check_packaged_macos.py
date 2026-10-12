"""Start the actual packaged Mac app and quit via its native application lifetime."""
import json
import pathlib
import subprocess
import sys
import tempfile
import time

assert sys.platform == 'darwin'
runtime = sys.argv[1]; assert runtime in {'osx-x64','osx-arm64'}
package = pathlib.Path('artifacts/release') / ('FocusOn-' + runtime)
with tempfile.TemporaryDirectory(prefix='focuson-mac-package-') as temporary:
    directory = pathlib.Path(temporary) / 'data'; directory.mkdir()
    subprocess.run(['ditto', '-x', '-k', str(package) + '.zip', temporary], check=True)
    bundle = pathlib.Path(temporary) / package.name / 'FocusOn.app'
    executable = bundle / 'Contents/MacOS/FocusOn'
    process = subprocess.Popen([str(executable), '--data-dir', str(directory)], stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
    try:
        database = pathlib.Path(directory) / 'focuson.db'
        for attempt in range(60):
            if process.poll() is not None: raise RuntimeError('Packaged Mac app exited during startup')
            if database.is_file(): break
            time.sleep(0.2)
        assert database.is_file(), 'Packaged Mac app did not initialize SQLite'
        # Send kAEQuitApplication to this exact process; avoids UI scripting/TCC and other installed instances.
        # AppleScript by bundle path addresses this validation bundle, never the user's installed copy.
        result = subprocess.run(['osascript', '-e', f'tell application "{bundle}" to quit'], capture_output=True, text=True, timeout=20)
        assert result.returncode == 0, 'Native Quit Apple event failed'
        process.wait(timeout=20); assert process.returncode == 0, 'Packaged app did not exit normally'
        print(json.dumps({'result':'PASS','runtime':runtime,'checks':['actual self-contained .app startup','SQLite initialized','native Quit event','normal exit']}))
    finally:
        if process.poll() is None: process.terminate(); process.wait(timeout=10)
