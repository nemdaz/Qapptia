from core import config, utils
from module_capture import constants
from module_capture.application.flow_capture_service import flow_capture_service


def register_flow_hotkey():
    hotkey = config.get("shortcut_flow")
    utils.register_hotkey(hotkey, flow_capture_service.toggle, constants.HOTKEY_DESCRIPTIONS["flow"])
