from core import config
from module_capture import constants
from module_capture.capture_dispatcher import request_capture
from core import utils


def register_screen_hotkey():
    hotkey = config.get("shortcut_screen")
    return utils.register_hotkey(hotkey, lambda: request_capture("screen", source="hotkey"), constants.HOTKEY_DESCRIPTIONS["screen"])
