"""An isolated real D-Bus StatusNotifier host; exercises registration and host loss."""
import dbus
import dbus.service
from dbus.mainloop.glib import DBusGMainLoop
from gi.repository import GLib
import os
import pathlib
import subprocess
import tempfile

DBusGMainLoop(set_as_default=True)
bus = dbus.SessionBus()
name = dbus.service.BusName('org.kde.StatusNotifierWatcher', bus)
interface = 'org.kde.StatusNotifierWatcher'

class Watcher(dbus.service.Object):
    def __init__(self):
        super().__init__(name, '/StatusNotifierWatcher')
        self.items = []
    @dbus.service.method(interface, in_signature='s', out_signature='')
    def RegisterStatusNotifierItem(self, service):
        if service not in self.items: self.items.append(str(service))
    @dbus.service.method('org.freedesktop.DBus.Properties', in_signature='ss', out_signature='v')
    def Get(self, iface, prop):
        assert iface == interface
        return self.properties()[prop]
    @dbus.service.method('org.freedesktop.DBus.Properties', in_signature='s', out_signature='a{sv}')
    def GetAll(self, iface):
        assert iface == interface
        return self.properties()
    def properties(self):
        return {'IsStatusNotifierHostRegistered': dbus.Boolean(True), 'RegisteredStatusNotifierItems': dbus.Array(self.items, signature='s'), 'ProtocolVersion': dbus.Int32(0)}

watcher = Watcher()
loop = GLib.MainLoop()
with tempfile.TemporaryDirectory(prefix='focuson-tray-host-') as directory:
    loss = pathlib.Path(directory) / 'loss'
    environment = dict(os.environ, FOCUSON_EXPECT_TRAY='1', FOCUSON_TRAY_LOSS_FILE=str(loss))
    process = subprocess.Popen(['bash', './build.sh', 'desktop-smoke'], env=environment)
    dropped = False
    def check():
        global dropped
        if loss.exists() and not dropped:
            watcher.remove_from_connection(); bus.release_name(interface); dropped = True
        if process.poll() is not None:
            loop.quit(); return False
        return True
    GLib.timeout_add(100, check)
    def timeout():
        process.kill(); loop.quit(); return False
    timer = GLib.timeout_add_seconds(90, timeout)
    try: loop.run()
    finally:
        if process.poll() is None: process.kill()
        process.wait()
    assert process.returncode == 0 and dropped, 'Native tray/host-loss smoke failed'
    print('PASS: real StatusNotifier registration, hide/restore, host disappearance and automatic recovery (isolated protocol host)')
