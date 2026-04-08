from core import config, utils
from core.logger import logger
from module_capture import constants
from module_capture.ui.area_selector import run_area_selector


def register_area_hotkey():
    hotkey = config.get("shortcut_area")
    return utils.register_hotkey(hotkey, trigger_area_capture, constants.HOTKEY_DESCRIPTIONS["area"])


def trigger_area_capture(callback=None):
    config.load_config()
    logger.info(constants.CAPTURE_MESSAGES["area_mode_start"])
    run_area_selector(on_capture_callback=callback)
