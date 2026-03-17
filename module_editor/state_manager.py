import json
import os
from core import config

STATE_FILE = "editor_state.json"

def get_state_path():
    base_path = os.path.expandvars(config.get("save_path"))
    if not os.path.exists(base_path):
        os.makedirs(base_path, exist_ok=True)
    return os.path.join(base_path, STATE_FILE)

def load_state():
    path = get_state_path()
    if os.path.exists(path):
        try:
            with open(path, "r", encoding="utf-8") as f:
                return json.load(f)
        except:
            pass
    return {"expanded_folders": [], "last_selected_file": None}

def save_state(state):
    path = get_state_path()
    try:
        with open(path, "w", encoding="utf-8") as f:
            json.dump(state, f, indent=4)
    except:
        pass

def update_expanded(folder_path, expanded):
    state = load_state()
    expanded_folders = set(state.get("expanded_folders", []))
    if expanded:
        expanded_folders.add(folder_path)
    else:
        expanded_folders.discard(folder_path)
    state["expanded_folders"] = list(expanded_folders)
    save_state(state)

def set_last_selected(file_path):
    state = load_state()
    state["last_selected_file"] = file_path
    save_state(state)
