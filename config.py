import json
import os
import winreg

CONFIG_FILE = "config.json"

def get_documents_dir():
    try:
        # Extraemos dinámicamente la carpeta del registro de Windows (Personal = My Documents)
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders") as key:
            docs_path, _ = winreg.QueryValueEx(key, "Personal")
            # Extraemos si es "Documentos", "Documents", u otro idioma
            folder_name = os.path.basename(docs_path)
            return os.path.join("%USERPROFILE%", folder_name, "QAScreens")
    except Exception:
        pass
    
    return os.path.join("%USERPROFILE%", "Documents", "QAScreens")

DEFAULT_CONFIG = {
    "save_path": get_documents_dir(),
    "filename_format": "Screenshot_YYYYMMDD_HHmmSS",
    "image_quality": 100,
    "subfolder_month": True,
    "subfolder_day": True,
    "subfolder_hour": False,
    "manual_timer": 0,
    "shortcut_key": "ctrl+shift+k",
    "flow_pause_key": "ctrl+shift"
}

current_config = DEFAULT_CONFIG.copy()

def load_config():
    global current_config
    if os.path.exists(CONFIG_FILE):
        try:
            with open(CONFIG_FILE, 'r', encoding='utf-8') as f:
                loaded = json.load(f)
                current_config.update(loaded)
        except Exception as e:
            print(f"Error loading config: {e}")
    else:
        save_config() # Create default file

def save_config():
    try:
        with open(CONFIG_FILE, 'w', encoding='utf-8') as f:
            json.dump(current_config, f, indent=4)
    except Exception as e:
        print(f"Error saving config: {e}")

def get(key):
    return current_config.get(key, DEFAULT_CONFIG.get(key))

def set(key, value):
    current_config[key] = value
    save_config()

# Load at import
load_config()
