import Gio from 'gi://Gio';
import Shell from 'gi://Shell';
import {Extension} from 'resource:///org/gnome/shell/extensions/extension.js';
import * as Main from 'resource:///org/gnome/shell/ui/main.js';

const iface = `<node><interface name="org.focuson.Observer"><method name="Snapshot"><arg type="s" direction="out"/></method></interface></node>`;

export default class FocusOnBridge extends Extension {
    enable() {
        this._object = Gio.DBusExportedObject.wrapJSObject(iface, this);
        this._object.export(Gio.DBus.session, '/org/focuson/Observer');
        this._name = Gio.bus_own_name_on_connection(Gio.DBus.session, 'org.focuson.Observer', Gio.BusNameOwnerFlags.NONE, null, null);
    }

    Snapshot() {
        const locked = Main.sessionMode.isLocked;
        const focused = locked ? null : global.display.focus_window;
        let window = null;
        if (focused) {
            const app = Shell.WindowTracker.get_default().get_window_app(focused);
            window = {
                id: focused.get_stable_sequence(),
                pid: Math.max(0, focused.get_pid()),
                key: app?.get_id() ? `desktop:${app.get_id()}` : null,
                name: app?.get_name() ?? null,
                title: focused.get_title() ?? null,
            };
        }
        // GNOME blanking/screen-shield state is session-wide, not per-monitor idle time.
        return JSON.stringify({window, locked, displayOff: Main.screenShield?.active ?? false});
    }

    disable() {
        this._object?.unexport();
        this._object = null;
        if (this._name) Gio.bus_unown_name(this._name);
        this._name = 0;
    }
}
