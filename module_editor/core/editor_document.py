from PIL import Image, ImageDraw

from module_editor import constants
from module_editor.core.tools import DrawingTool
from module_editor.core.vector_store import create_vector, vector_store


class EditorDocument:
    def __init__(self, store=vector_store):
        self._store = store
        self.image_path = None
        self.image = None
        self.vectors = []

    def load(self, image, image_path):
        self.image = image
        self.image_path = image_path
        self.vectors = self._store.load(image_path)

    def create_vector(self, vector_type, point, color):
        vector = create_vector(vector_type, point, color)
        self.vectors.append(vector)
        return vector

    def delete_vector(self, shape_id):
        self.vectors = [vector for vector in self.vectors if vector.shape_id != shape_id]

    def update_vector_color(self, shape_id, color):
        for vector in self.vectors:
            if vector.shape_id == shape_id:
                vector.color = color
                return True
        return False

    def save_vectors(self):
        self._store.save(self.image_path, self.vectors)

    def get_composite_image(self):
        if self.image is None:
            return None

        base = self.image.copy().convert("RGBA")
        overlay = Image.new("RGBA", base.size, (0, 0, 0, 0))
        draw = ImageDraw.Draw(overlay)

        for vector in self.vectors:
            DrawingTool.render_pil(draw, vector.shape_type, vector.coords, vector.color, constants.VECTOR_STYLE["stroke_width"])

        return Image.alpha_composite(base, overlay)
