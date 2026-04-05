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
