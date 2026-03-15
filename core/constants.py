import os

CONFIG_FILE = "config.json"

DEFAULT_CONFIG = {
    "save_path": os.path.expandvars(os.path.join("%USERPROFILE%", "QAScreens")),
    "filename_format": "Screenshot_YYYYMMDD_HHmmSS",
    "image_quality": 100,
    "subfolder_month": True,
    "subfolder_day": True,
    "subfolder_hour": False,
    "show_mouse": True,
    "highlight_mouse": False,
    "manual_timer": 0,
    "shortcut_key": "ctrl+shift+k",
    "flow_pause_key": "ctrl+shift"
}
