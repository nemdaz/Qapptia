from abc import ABC, abstractmethod


class InputService(ABC):
    @abstractmethod
    def requires_process_restart_after_resume(self):
        pass

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
    def remove_hotkey(self, hotkey_handle):
        pass

    @abstractmethod
    def unhook_all_hotkeys(self):
        pass

    @abstractmethod
    def on_press_key(self, key, callback, suppress=False):
        pass

    @abstractmethod
    def unhook_key_listener(self, hook):
        pass

    @abstractmethod
    def restore_global_hooks_after_resume(self, register_hotkeys_callback, mouse_callback, max_attempts=2, retry_delay_seconds=0.25):
        pass


class DpiService(ABC):
    @abstractmethod
    def set_process_dpi_awareness(self):
        pass


class ProcessService(ABC):
    @abstractmethod
    def acquire_single_instance(self, key):
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
    def show_info_message(self, title, message):
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
