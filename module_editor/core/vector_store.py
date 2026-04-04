import json
import os
from uuid import uuid4

from core.logger import logger
from module_editor.core.models import VectorShape


def create_vector(vector_type, point, color):
    return VectorShape(
        shape_type=vector_type,
        shape_id=f"{vector_type}_{uuid4().hex}",
        coords=[point.x(), point.y(), point.x(), point.y()],
        color=color,
    )


class VectorStore:
    def get_json_path(self, image_path):
        if not image_path:
            return None
        return os.path.splitext(image_path)[0] + ".json"

    def load(self, image_path):
        path = self.get_json_path(image_path)
        if path and os.path.exists(path):
            try:
                with open(path, "r", encoding="utf-8") as f:
                    return [VectorShape.from_dict(item) for item in json.load(f)]
            except Exception as exc:
                logger.error(f"Error loading vectors from {path}: {exc}")
        return []

    def save(self, image_path, vectors):
        path = self.get_json_path(image_path)
        if not path:
            return
        try:
            with open(path, "w", encoding="utf-8") as f:
                json.dump([vector.to_dict() for vector in vectors], f, indent=4)
        except Exception as exc:
            logger.error(f"Error saving vectors to {path}: {exc}")


vector_store = VectorStore()
