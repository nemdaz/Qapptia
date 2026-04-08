import atexit
import os
import tempfile

import keyboard
import mouse
import pystray
from PIL import Image, ImageDraw, ImageGrab

from core.platform.base import DesktopService, DpiService, InputService, ProcessService, ScreenService, TrayService


class _PosixInstanceGuard:
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


class MacOsInputService(InputService):
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

    def unhook_all_hotkeys(self):
        keyboard.unhook_all_hotkeys()

    def on_press_key(self, key, callback, suppress=False):
        return keyboard.on_press_key(key, callback, suppress=suppress)

    def unhook_key_listener(self, hook):
        keyboard.unhook(hook)


class MacOsDpiService(DpiService):
    def set_process_dpi_awareness(self):
        return None


class MacOsProcessService(ProcessService):
    def acquire_single_instance(self, key):
        guard = _PosixInstanceGuard(key)
        if not guard.acquire():
            return None
        atexit.register(guard.release)
        return guard


class MacOsScreenService(ScreenService):
    def capture_all_screens(self):
        return ImageGrab.grab(all_screens=True)


class MacOsDesktopService(DesktopService):
    def play_beep(self, _sound_path):
        print("\a", end="", flush=True)

    def show_info_message(self, title, message):
        print(f"{title}: {message}", flush=True)

    def get_dpi_scaling(self):
        return 1.0

    def get_monitor_at_cursor(self):
        return 0, 0, 1920, 1080

    def get_virtual_screen_origin(self):
        return 0, 0

    def get_current_cursor(self, scale):
        return self._get_fallback_cursor(scale), (0, 0)

    def _get_fallback_cursor(self, scale):
        base_points = [(0, 0), (0, 16), (4, 12), (7, 19), (9, 18), (6, 11), (11, 11)]
        ss = 4
        canvas_scale = scale * ss
        w, h = int(16 * canvas_scale), int(24 * canvas_scale)
        temp_img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        draw = ImageDraw.Draw(temp_img)
        scaled_points = [(p[0] * canvas_scale, p[1] * canvas_scale) for p in base_points]
        draw.polygon(scaled_points, fill="white", outline="black")
        final_w, final_h = int(w / ss), int(h / ss)
        return temp_img.resize((final_w, final_h), Image.LANCZOS)


class MacOsTrayService(TrayService):
    def menu_item(self, title_or_callable, callback, default=False, visible=True):
        return pystray.MenuItem(title_or_callable, callback, default=default, visible=visible)

    def menu_separator(self):
        return pystray.Menu.SEPARATOR

    def menu(self, *items):
        return pystray.Menu(*items)

    def icon(self, name, image, title, menu):
        return pystray.Icon(name, image, title, menu)
