from core import config, utils
from module_capture import constants
from module_capture.application.fullscreen_capture_service import fullscreen_capture_service


def register_screen_hotkey():
    hotkey = config.get("shortcut_screen")
    return utils.register_hotkey(hotkey, trigger_screen_capture, constants.HOTKEY_DESCRIPTIONS["screen"])


def trigger_screen_capture():
    return fullscreen_capture_service.capture_with_timer()
