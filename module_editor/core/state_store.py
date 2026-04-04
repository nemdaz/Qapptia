import json
import os

from core import config
from core.logger import logger
from module_editor.core.models import EditorPreferences

STATE_FILE = "editor_state.json"


class EditorStateStore:
    def __init__(self, state_file=STATE_FILE):
        self._state_file = state_file

    def get_state_path(self):
        base_path = os.path.expandvars(config.get("save_path"))
        os.makedirs(base_path, exist_ok=True)
        return os.path.join(base_path, self._state_file)

    def load(self):
        path = self.get_state_path()
        if not os.path.exists(path):
            return EditorPreferences()

        try:
            with open(path, "r", encoding="utf-8") as f:
                loaded_state = json.load(f)
        except json.JSONDecodeError as exc:
            logger.warning(f"Editor state is invalid JSON at {path}: {exc}")
            return EditorPreferences()
        except OSError as exc:
            logger.error(f"Unable to read editor state from {path}: {exc}")
            return EditorPreferences()

        return EditorPreferences.from_dict(loaded_state)

    def save(self, state):
        path = self.get_state_path()
        payload = state.to_dict()

        try:
            with open(path, "w", encoding="utf-8") as f:
                json.dump(payload, f, indent=4)
        except OSError as exc:
            logger.error(f"Unable to save editor state to {path}: {exc}")

    def mutate(self, mutator):
        state = self.load()
        mutator(state)
        self.save(state)
        return state


state_store = EditorStateStore()
