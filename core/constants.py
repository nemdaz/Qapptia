import os
from core.version import VERSION, APP_NAME


# El nombre de la aplicación se centraliza en core.version
CONFIG_FILE = "config.json"

# Hotkeys internos (no configurables por usuario)
INTERNAL_CONFIG = {
    "shortcut_copy_clipboard": "ctrl+c",
    "editor_tool_text_default_width": 240,
    "editor_tool_text_default_height": 28,
    "shutter_sound": os.path.join("core", "assets", "sounds", "shutter_a.wav"),
}

# Hotkeys configurables por usuario y otros valores por defecto
DEFAULT_CONFIG = {
    "save_path": os.path.expandvars(os.path.join("%USERPROFILE%", APP_NAME)),
    "filename_format": "QACappta_YYYYMMDD_HHmmSS",
    "subfolder_month": True,
    "subfolder_day": True,
    "subfolder_hour": False,
    "show_mouse": True,
    "highlight_mouse": False,
    "manual_timer": 0,
    "shortcut_screen": "ctrl+shift+q",
    "shortcut_area": "ctrl+shift+a",
    "shortcut_flow": "ctrl+shift+f",
    "shortcut_flow_pause": "ctrl+shift",
    "enable_scroll_capture": True,
    "copy_to_clipboard_screen": False,
    "copy_to_clipboard_area": False,
}


RUNTIME_CONFIG = {
    "main_loop_sleep_seconds": 1.0,
    "suspend_jump_threshold_seconds": 10.0,
    "hook_recovery_max_attempts": 2,
    "hook_recovery_retry_delay_seconds": 0.25,
    "restart_grace_period_seconds": 0.5,
    "editor_double_click_seconds": 0.4,
    "editor_launch_guard_seconds": 5.0,
}
