import json
import os
from constants import CONFIG_FILE, DEFAULT_CONFIG

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
