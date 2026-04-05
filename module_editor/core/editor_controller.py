import os

from module_editor import constants
from module_editor.core.editor_document import EditorDocument
from module_editor.core.image_session import ImageSession
from module_editor.core.state_store import state_store


class EditorController:
    def __init__(self):
        self._image_session = ImageSession()
        self.document = EditorDocument()
        self.preferences = state_store.load()
        self.active_tool = None

    @property
    def current_color_name(self):
        return self.preferences.active_fav_color

    @property
    def current_color_hex(self):
        return constants.FAVORITE_COLORS.get(self.current_color_name, constants.FAVORITE_COLORS[constants.DEFAULT_FAV_COLOR])

    @property
    def current_image_path(self):
        return self._image_session.image_path

    @property
    def current_rotation(self):
        return self._image_session.rotation

    @property
    def has_unsaved_rotation(self):
        return self._image_session.rotation != 0

    @property
    def has_pending_save(self):
        return self.has_unsaved_rotation or self.document.has_vectors()

    def set_active_color(self, color_name):
        self.preferences.active_fav_color = color_name
        state_store.save(self.preferences)

    def set_active_tool(self, tool):
        self.active_tool = None if self.active_tool == tool else tool
        return self.active_tool

    def open_image(self, path):
        if not os.path.exists(path):
            return None

        normalized_path = path.replace("\\", "/")
        if self.current_image_path == normalized_path:
            return None

        display_image = self._image_session.open(normalized_path)
        self.document.load(display_image, self.current_image_path)
        self.preferences.last_selected_file = self.current_image_path
        state_store.save(self.preferences)
        return display_image

    def load_initial_image_path(self):
        last = self.preferences.last_selected_file
        if last and os.path.exists(last):
            return last
        return None

    def restore_last_image(self):
        last = self.load_initial_image_path()
        if not last:
            return None
        return self.open_image(last)

    def rotate_image(self):
        return self._image_session.rotate_clockwise()

    def save_rotation(self):
        if not self.has_unsaved_rotation:
            return None
        self._image_session.save_rotation()
        display_image = self._image_session.get_display_image()
        self.document.load(display_image, self.current_image_path)
        return display_image

    def save_current_image(self):
        if not self.current_image_path or not self.has_pending_save:
            return None

        composite_image = self.document.get_composite_image()
        if composite_image is None:
            return None

        self._image_session.save_image(composite_image)
        self.document.clear_vectors()

        display_image = self._image_session.get_display_image()
        self.document.load(display_image, self.current_image_path)
        return display_image

    def copy_file_path(self):
        return self.current_image_path

    def close(self):
        self._image_session.close()
