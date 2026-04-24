from core import config
from module_capture import constants
from module_capture.capture_dispatcher import request_capture
from core import utils


def register_flow_hotkey():
    hotkey = config.get("shortcut_flow")
    return utils.register_hotkey(hotkey, lambda: request_capture("flow", source="hotkey"), constants.HOTKEY_DESCRIPTIONS["flow"])
