from PIL import Image


class ImageSession:
    def __init__(self):
        self.image_path = None
        self._base_image = None
        self.rotation = 0

    @property
    def has_image(self):
        return self._base_image is not None and self.image_path is not None

    def open(self, image_path):
        normalized_path = image_path.replace("\\", "/")
        self.close()
        with Image.open(normalized_path) as img:
            self._base_image = img.copy()
        self.image_path = normalized_path
        self.rotation = 0
        return self._base_image.copy()

    def get_display_image(self):
        if not self.has_image:
            return None
        if self.rotation == 0:
            return self._base_image.copy()
        return self._base_image.rotate(-self.rotation, expand=True)

    def rotate_clockwise(self, degrees=90):
        if not self.has_image:
            return None
        self.rotation = (self.rotation + degrees) % 360
        return self.get_display_image()

    def save_rotation(self):
        if not self.has_image or self.rotation == 0:
            return False

        rotated = self.get_display_image()
        self.save_image(rotated)
        return True

    def save_image(self, image):
        if not self.image_path or image is None:
            return False

        output_image = image
        extension = Image.EXTENSION.get(f'.{self.image_path.rsplit('.', 1)[-1].lower()}')
        if extension in {"JPEG", "BMP"} and image.mode == "RGBA":
            output_image = image.convert("RGB")

        output_image.save(self.image_path)

        if self._base_image is not None:
            self._base_image.close()

        self._base_image = output_image.copy()
        self.rotation = 0
        return True

    def close(self):
        if self._base_image is not None:
            self._base_image.close()
            self._base_image = None
        self.image_path = None
        self.rotation = 0
