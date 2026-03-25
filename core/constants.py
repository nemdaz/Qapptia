import os
from core.version import VERSION, APP_NAME

# El nombre de la aplicación se centraliza en core.version
CONFIG_FILE = "config.json"

DEFAULT_CONFIG = {
    "save_path": os.path.expandvars(os.path.join("%USERPROFILE%", APP_NAME)),
    "filename_format": "Screenshot_YYYYMMDD_HHmmSS",
    "image_quality": 100,
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
    "enable_scroll_capture": True
}
