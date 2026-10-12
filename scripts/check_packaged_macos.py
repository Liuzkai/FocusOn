"""Start the actual packaged Mac app and quit via its native application lifetime."""
import json
import ctypes
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
        # Direct launch has no LaunchServices path registration. Target the actual PID.
        class Desc(ctypes.Structure):
            _fields_ = [('type',ctypes.c_uint32),('handle',ctypes.c_void_p)]
        ae = ctypes.CDLL('/System/Library/Frameworks/CoreServices.framework/CoreServices')
        ae.AECreateDesc.argtypes = [ctypes.c_uint32,ctypes.c_void_p,ctypes.c_long,ctypes.POINTER(Desc)]
        ae.AECreateAppleEvent.argtypes = [ctypes.c_uint32,ctypes.c_uint32,ctypes.POINTER(Desc),ctypes.c_int16,ctypes.c_int32,ctypes.POINTER(Desc)]
        ae.AESendMessage.argtypes = [ctypes.POINTER(Desc),ctypes.POINTER(Desc),ctypes.c_int32,ctypes.c_int32]
        ae.AEDisposeDesc.argtypes = [ctypes.POINTER(Desc)]
        def code(text):return int.from_bytes(text.encode(),'big')
        target,event,reply = Desc(),Desc(),Desc(); pid = ctypes.c_int32(process.pid)
        try:
            assert ae.AECreateDesc(code('kpid'),ctypes.byref(pid),4,ctypes.byref(target)) == 0
            assert ae.AECreateAppleEvent(code('aevt'),code('quit'),ctypes.byref(target),-1,0,ctypes.byref(event)) == 0
            # -600 means the still-starting process has not registered its Apple-event
            # endpoint yet. Retry this specific startup state, never a permission/error denial.
            for attempt in range(75):
                assert process.poll() is None, 'Packaged app exited before native Quit became available'
                status = ae.AESendMessage(ctypes.byref(event),ctypes.byref(reply),1 | 0x10,-1)
                if status != -600: break
                time.sleep(0.2)
            assert status == 0, 'Native PID Quit Apple event failed: OSStatus ' + str(status)
        finally:
            for descriptor in [reply,event,target]: ae.AEDisposeDesc(ctypes.byref(descriptor))
        process.wait(timeout=20); assert process.returncode == 0, 'Packaged app did not exit normally'
        print(json.dumps({'result':'PASS','runtime':runtime,'checks':['actual self-contained .app startup','SQLite initialized','native Quit event','normal exit']}))
    finally:
        if process.poll() is None: process.terminate(); process.wait(timeout=10)
