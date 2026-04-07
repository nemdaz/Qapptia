from abc import ABC, abstractmethod


class InputService(ABC):
    @abstractmethod
    def hook_mouse(self, callback):
        pass

    @abstractmethod
    def unhook_all_mouse(self):
        pass

    @abstractmethod
    def get_mouse_position(self):
        pass

    @abstractmethod
    def is_mouse_button_event(self, event):
        pass

    @abstractmethod
    def is_mouse_wheel_event(self, event):
        pass

    @abstractmethod
    def is_mouse_move_event(self, event):
        pass

    @abstractmethod
    def is_key_pressed(self, key):
        pass

    @abstractmethod
    def add_hotkey(self, hotkey, callback, suppress=False):
        pass

    @abstractmethod
    def on_press_key(self, key, callback, suppress=False):
        pass

    @abstractmethod
    def unhook_key_listener(self, hook):
        pass


class DpiService(ABC):
    @abstractmethod
    def set_process_dpi_awareness(self):
        pass


class ScreenService(ABC):
    @abstractmethod
    def capture_all_screens(self):
        pass


class DesktopService(ABC):
    @abstractmethod
    def play_beep(self, sound_path):
        pass

    @abstractmethod
    def get_dpi_scaling(self):
        pass

    @abstractmethod
    def get_monitor_at_cursor(self):
        pass

    @abstractmethod
    def get_virtual_screen_origin(self):
        pass

    @abstractmethod
    def get_current_cursor(self, scale):
        pass


class TrayService(ABC):
    @abstractmethod
    def menu_item(self, title_or_callable, callback, default=False, visible=True):
        pass

    @abstractmethod
    def menu_separator(self):
        pass

    @abstractmethod
    def menu(self, *items):
        pass

    @abstractmethod
    def icon(self, name, image, title, menu):
        pass
