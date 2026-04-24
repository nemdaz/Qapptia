from core import config
from core.utils import register_hotkey
from module_capture import constants
from module_capture.capture_dispatcher import request_capture


def register_all_capture_hotkeys():
    config.load_config()
    screen_ok = _register_mode("screen")
    area_ok = _register_mode("area")
    flow_ok = _register_mode("flow")
    return bool(screen_ok and area_ok and flow_ok)


def _register_mode(mode):
    hotkey = config.get(f"shortcut_{mode}")
    return register_hotkey(
        hotkey,
        lambda m=mode: request_capture(m, source="hotkey"),
        constants.HOTKEY_DESCRIPTIONS[mode],
    )
