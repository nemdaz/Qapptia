import atexit
import os
import tempfile

import keyboard
import mouse
import pystray
from PIL import Image, ImageDraw, ImageGrab
from PySide6.QtCore import QRect
from PySide6.QtGui import QCursor
from PySide6.QtWidgets import QApplication

from core.input_runtime import restore_global_input_hooks_in_process


_qt_application_holder = None


def _ensure_qt_application():
    global _qt_application_holder

    app = QApplication.instance()
    if app is not None:
        return app

    _qt_application_holder = QApplication([])
    _qt_application_holder.setQuitOnLastWindowClosed(False)
    return _qt_application_holder


def _get_virtual_geometry(app):
    screens = app.screens()
    if not screens:
        return QRect(0, 0, 1, 1)

    geometry = screens[0].geometry()
    for screen in screens[1:]:
        geometry = geometry.united(screen.geometry())
    return geometry


def _get_screen_at_cursor(app):
    cursor_position = QCursor.pos()
    if cursor_position is not None:
        screen = app.screenAt(cursor_position)
        if screen is not None:
            return screen

    primary = app.primaryScreen()
    if primary is not None:
        return primary

    screens = app.screens()
    if screens:
        return screens[0]
    return None


class _UnixInstanceGuard:
    def __init__(self, key):
        self.key = key
        self._lock_fd = None
        self._lock_path = os.path.join(tempfile.gettempdir(), f"qascreenshot_{self.key}.lock")
        self._released = False

    def acquire(self):
        for _attempt in range(2):
            try:
                self._lock_fd = os.open(self._lock_path, os.O_CREAT | os.O_EXCL | os.O_RDWR)
                os.write(self._lock_fd, str(os.getpid()).encode("utf-8"))
                return True
            except FileExistsError:
                if not self._remove_stale_lock_file():
                    return False
            except OSError:
                return False
        return False

    def release(self):
        if self._released:
            return
        self._released = True
        if self._lock_fd is not None:
            try:
                os.close(self._lock_fd)
            except OSError:
                pass
            self._lock_fd = None
        try:
            os.unlink(self._lock_path)
        except FileNotFoundError:
            pass
        except OSError:
            pass

    def _remove_stale_lock_file(self):
        try:
            with open(self._lock_path, "r", encoding="utf-8") as lock_file:
                pid = int(lock_file.read().strip())
        except (OSError, ValueError):
            pid = None

        if pid and _is_process_running(pid):
            return False

        try:
            os.unlink(self._lock_path)
            return True
        except FileNotFoundError:
            return True
        except OSError:
            return False


def _is_process_running(pid):
    try:
        os.kill(pid, 0)
    except OSError:
        return False
    return True


class UnixInputMixin:
    def hook_mouse(self, callback):
        return mouse.hook(callback)

    def unhook_all_mouse(self):
        mouse.unhook_all()

    def get_mouse_position(self):
        return mouse.get_position()

    def is_mouse_button_event(self, event):
        return isinstance(event, mouse.ButtonEvent)

    def is_mouse_wheel_event(self, event):
        return isinstance(event, mouse.WheelEvent)

    def is_mouse_move_event(self, event):
        return isinstance(event, mouse.MoveEvent)

    def is_key_pressed(self, key):
        return keyboard.is_pressed(key)

    def add_hotkey(self, hotkey, callback, suppress=False):
        return keyboard.add_hotkey(hotkey, callback, suppress=suppress)

    def remove_hotkey(self, hotkey_handle):
        keyboard.remove_hotkey(hotkey_handle)

    def unhook_all_hotkeys(self):
        keyboard.unhook_all_hotkeys()

    def on_press_key(self, key, callback, suppress=False):
        return keyboard.on_press_key(key, callback, suppress=suppress)

    def unhook_key_listener(self, hook):
        keyboard.unhook(hook)

    def restore_global_hooks_after_resume(self, register_hotkeys_callback, mouse_callback, max_attempts=2, retry_delay_seconds=0.25):
        return restore_global_input_hooks_in_process(
            input_service=self,
            register_hotkeys_callback=register_hotkeys_callback,
            mouse_callback=mouse_callback,
            max_attempts=max_attempts,
            retry_delay_seconds=retry_delay_seconds,
        )


class UnixDpiMixin:
    def set_process_dpi_awareness(self):
        return None


class UnixProcessMixin:
    def acquire_single_instance(self, key):
        guard = _UnixInstanceGuard(key)
        if not guard.acquire():
            return None
        atexit.register(guard.release)
        return guard


class UnixScreenMixin:
    def capture_all_screens(self):
        return ImageGrab.grab(all_screens=True)


class UnixDesktopMixin:
    def show_info_message(self, title, message):
        print(f"{title}: {message}", flush=True)

    def get_dpi_scaling(self):
        app = _ensure_qt_application()
        screen = _get_screen_at_cursor(app)
        if screen is None:
            return 1.0

        ratio = float(screen.devicePixelRatio() or 1.0)
        return ratio if ratio > 0 else 1.0

    def get_monitor_at_cursor(self):
        app = _ensure_qt_application()
        screen = _get_screen_at_cursor(app)
        if screen is None:
            return 0, 0, 1, 1

        geometry = screen.geometry()
        return geometry.x(), geometry.y(), geometry.width(), geometry.height()

    def get_virtual_screen_origin(self):
        app = _ensure_qt_application()
        geometry = _get_virtual_geometry(app)
        return geometry.x(), geometry.y()

    def get_current_cursor(self, scale):
        return self._get_fallback_cursor(scale), (0, 0)

    def _get_fallback_cursor(self, scale):
        base_points = [(0, 0), (0, 16), (4, 12), (7, 19), (9, 18), (6, 11), (11, 11)]
        ss = 4
        canvas_scale = scale * ss
        width, height = int(16 * canvas_scale), int(24 * canvas_scale)
        temp_img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        draw = ImageDraw.Draw(temp_img)
        scaled_points = [(point[0] * canvas_scale, point[1] * canvas_scale) for point in base_points]
        draw.polygon(scaled_points, fill="white", outline="black")
        final_width, final_height = int(width / ss), int(height / ss)
        return temp_img.resize((final_width, final_height), Image.LANCZOS)


class UnixTrayMixin:
    def menu_item(self, title_or_callable, callback, default=False, visible=True):
        return pystray.MenuItem(title_or_callable, callback, default=default, visible=visible)

    def menu_separator(self):
        return pystray.Menu.SEPARATOR

    def menu(self, *items):
        return pystray.Menu(*items)

    def icon(self, name, image, title, menu):
        return pystray.Icon(name, image, title, menu)