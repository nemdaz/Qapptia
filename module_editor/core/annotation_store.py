import json
import os
import shutil
from uuid import uuid4

from core.logger import logger
from module_editor.constants import ANNOTATION_DIR
from module_editor.core.annotation_models import VectorShape


def create_vector(vector_type, point, color, payload=None):
    return VectorShape(
        shape_type=vector_type,
        shape_id=f"{vector_type}_{uuid4().hex}",
        coords=[point.x(), point.y(), point.x(), point.y()],
        color=color,
        payload=dict(payload or {}),
    )


class VectorStore:
    def get_json_path(self, image_path):
        if not image_path:
            return None
        parent_dir = os.path.dirname(image_path)
        base_name = os.path.splitext(os.path.basename(image_path))[0]
        annotation_dir = os.path.join(parent_dir, ANNOTATION_DIR)
        return os.path.join(annotation_dir, base_name + ".json")

    @staticmethod
    def _legacy_json_path(image_path):
        if not image_path:
            return None
        return os.path.splitext(image_path)[0] + ".json"

    def exists(self, image_path):
        path = self.get_json_path(image_path)
        return bool(path and os.path.exists(path))

    def load(self, image_path):
        self._migrate_legacy_json(image_path)
        path = self.get_json_path(image_path)
        if path and os.path.exists(path):
            try:
                with open(path, "r", encoding="utf-8") as f:
                    return [VectorShape.from_dict(item) for item in json.load(f)]
            except Exception as exc:
                logger.error(f"Error loading vectors from {path}: {exc}")
        return []

    def _migrate_legacy_json(self, image_path):
        new_path = self.get_json_path(image_path)
        old_path = self._legacy_json_path(image_path)
        if not new_path or not old_path:
            return
        new_exists = os.path.exists(new_path)
        old_exists = os.path.exists(old_path)
        if not old_exists:
            return
        if new_exists:
            try:
                os.remove(old_path)
                logger.debug(f"Migracion: eliminado JSON legado '{old_path}' (prevalece '{new_path}')")
            except OSError as exc:
                logger.error(f"Migracion: error al eliminar JSON legado '{old_path}': {exc}")
        else:
            try:
                os.makedirs(os.path.dirname(new_path), exist_ok=True)
                shutil.move(old_path, new_path)
                logger.debug(f"Migracion: movido JSON legado '{old_path}' -> '{new_path}'")
            except (OSError, shutil.Error) as exc:
                logger.error(f"Migracion: error al mover JSON legado '{old_path}' -> '{new_path}': {exc}")

    def save(self, image_path, vectors):
        path = self.get_json_path(image_path)
        if not path:
            return
        if not vectors:
            self.delete(image_path)
            return
        try:
            os.makedirs(os.path.dirname(path), exist_ok=True)
            with open(path, "w", encoding="utf-8") as f:
                json.dump([vector.to_dict() for vector in vectors], f, indent=4)
        except Exception as exc:
            logger.error(f"Error saving vectors to {path}: {exc}")

    def delete(self, image_path):
        path = self.get_json_path(image_path)
        if not path or not os.path.exists(path):
            return
        try:
            os.remove(path)
        except OSError as exc:
            logger.error(f"Error deleting vectors from {path}: {exc}")


vector_store = VectorStore()
