from core import config
from module_capture import constants
from module_capture.domain.capture_settings import CaptureSettings


class CaptureSettingsRepository:
    def load_settings(self):
        config.load_config()
        return CaptureSettings(
            save_path=config.get("save_path"),
            filename_format=config.get("filename_format"),
            image_quality=int(config.get("image_quality")),
            subfolder_month=bool(config.get("subfolder_month")),
            subfolder_day=bool(config.get("subfolder_day")),
            subfolder_hour=bool(config.get("subfolder_hour")),
            show_mouse=bool(config.get("show_mouse")),
            highlight_mouse=bool(config.get("highlight_mouse")),
            manual_timer=int(config.get("manual_timer")),
            shortcut_screen=(config.get("shortcut_screen") or constants.CAPTURE_DEFAULTS["shortcuts"]["shortcut_screen"]).lower(),
            shortcut_area=(config.get("shortcut_area") or constants.CAPTURE_DEFAULTS["shortcuts"]["shortcut_area"]).lower(),
            shortcut_flow=(config.get("shortcut_flow") or constants.CAPTURE_DEFAULTS["shortcuts"]["shortcut_flow"]).lower(),
            shortcut_flow_pause=(config.get("shortcut_flow_pause") or constants.CAPTURE_DEFAULTS["shortcuts"]["shortcut_flow_pause"]).lower(),
            enable_scroll_capture=bool(config.get("enable_scroll_capture")),
        )

    def save_settings(self, settings):
        for key, value in settings.to_config_payload().items():
            config.set(key, value)

capture_settings_repository = CaptureSettingsRepository()
