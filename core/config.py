import json
import os
from core.logger import logger
from core.constants import CONFIG_FILE, DEFAULT_CONFIG

current_config = DEFAULT_CONFIG.copy()


def _normalize_config_value(default_value, raw_value):
    if isinstance(default_value, bool):
        if isinstance(raw_value, bool):
            return raw_value
        if isinstance(raw_value, str):
            lowered = raw_value.strip().lower()
            if lowered in ("1", "true", "yes", "on"):
                return True
            if lowered in ("0", "false", "no", "off"):
                return False
        if isinstance(raw_value, (int, float)):
            return bool(raw_value)
        return default_value

    if isinstance(default_value, int):
        if isinstance(raw_value, bool):
            return int(raw_value)
        try:
            return int(raw_value)
        except (TypeError, ValueError):
            return default_value

    if isinstance(default_value, str):
        if raw_value is None:
            return default_value
        return str(raw_value)

    return raw_value if raw_value is not None else default_value


def _build_config_from_defaults(loaded):
    merged = DEFAULT_CONFIG.copy()
    if not isinstance(loaded, dict):
        return merged

    for key, default_value in DEFAULT_CONFIG.items():
        if key not in loaded:
            continue
        merged[key] = _normalize_config_value(default_value, loaded.get(key))

    return merged

def load_config():
    global current_config
    current_config = DEFAULT_CONFIG.copy()

    if os.path.exists(CONFIG_FILE):
        try:
            with open(CONFIG_FILE, 'r', encoding='utf-8') as f:
                loaded = json.load(f)
                current_config = _build_config_from_defaults(loaded)
        except Exception as e:
            logger.error(f"Error loading config: {e}")
    else:
        save_config()  # Create default file

def save_config():
    try:
        with open(CONFIG_FILE, 'w', encoding='utf-8') as f:
            json.dump(current_config, f, indent=4)
    except Exception as e:
        logger.error(f"Error saving config: {e}")

def get(key):
    return current_config.get(key, DEFAULT_CONFIG.get(key))

def set(key, value):
    current_config[key] = value
    save_config()


def replace(values):
    global current_config
    current_config = _build_config_from_defaults(values)
    save_config()

# Load at import
load_config()
