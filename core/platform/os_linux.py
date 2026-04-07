import keyboard
import mouse
import pystray
from PIL import Image, ImageDraw, ImageGrab
import subprocess
import os

from core.platform.base import DesktopService, DpiService, InputService, ScreenService, TrayService


class LinuxInputService(InputService):
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


class LinuxDpiService(DpiService):
    def set_process_dpi_awareness(self):
        return None


class LinuxScreenService(ScreenService):
    def capture_all_screens(self):
        return ImageGrab.grab(all_screens=True)


class LinuxDesktopService(DesktopService):
    def play_beep(self, _sound_path):
        print("\a", end="", flush=True)

    def get_dpi_scaling(self):
        try:
            if "GDK_SCALE" in os.environ:
                return float(os.environ["GDK_SCALE"])
            subprocess.check_output(["xrandr", "--current"], stderr=subprocess.STDOUT)
        except Exception:
            pass
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


class LinuxTrayService(TrayService):
    def menu_item(self, title_or_callable, callback, default=False, visible=True):
        return pystray.MenuItem(title_or_callable, callback, default=default, visible=visible)

    def menu_separator(self):
        return pystray.Menu.SEPARATOR

    def menu(self, *items):
        return pystray.Menu(*items)

    def icon(self, name, image, title, menu):
        return pystray.Icon(name, image, title, menu)
