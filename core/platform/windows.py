import ctypes

import keyboard
import mouse
from PIL import ImageGrab

from core.platform.base import DpiService, InputService, ScreenService


class WindowsInputService(InputService):
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

    def on_press_key(self, key, callback, suppress=False):
        return keyboard.on_press_key(key, callback, suppress=suppress)

    def unhook_key_listener(self, hook):
        keyboard.unhook(hook)


class WindowsDpiService(DpiService):
    def set_process_dpi_awareness(self):
        try:
            ctypes.windll.shcore.SetProcessDpiAwareness(2)
            return
        except Exception:
            pass

        try:
            ctypes.windll.user32.SetProcessDPIAware()
        except Exception:
            pass


class WindowsScreenService(ScreenService):
    def capture_all_screens(self):
        return ImageGrab.grab(all_screens=True)
