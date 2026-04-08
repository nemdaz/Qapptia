import os
from dataclasses import dataclass

from module_editor import constants
from module_editor.core.annotation_document import EditorDocument
from module_editor.core.image_session import ImageSession
from module_editor.core.preferences_store import state_store


@dataclass(frozen=True)
class ToolbarState:
    active_tool: str | None
    color_name: str | None
    color_hex: str
    draw_mode: str | None
    draw_cursor_active: bool


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
        return self.color_hex(self.current_color_name)

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

    def color_hex(self, color_name):
        return constants.FAVORITE_COLORS.get(color_name, constants.FAVORITE_COLORS[constants.DEFAULT_FAV_COLOR])

    def favorite_color_name_for_hex(self, color_hex):
        normalized = color_hex.lower()
        for color_name, hex_value in constants.FAVORITE_COLORS.items():
            if hex_value.lower() == normalized:
                return color_name
        return None

    def remember_color(self, color_name, tools=None):
        self.preferences.active_fav_color = color_name
        if tools is None:
            tools = [self.active_tool] if self.active_tool in constants.VECTOR_COLOR_TOOLS else []

        for tool in tools:
            if tool in constants.VECTOR_COLOR_TOOLS:
                self.preferences.tool_fav_colors[tool] = color_name
        state_store.save(self.preferences)

    def color_name_for_tool(self, tool):
        if tool not in constants.VECTOR_COLOR_TOOLS:
            return self.preferences.active_fav_color
        return self.preferences.tool_fav_colors.get(tool, self.preferences.active_fav_color)

    def _build_toolbar_state(self, active_tool, color_name=None, color_hex=None):
        resolved_color_name = color_name
        resolved_color_hex = color_hex

        if resolved_color_name is not None and resolved_color_hex is None:
            resolved_color_hex = self.color_hex(resolved_color_name)
        elif resolved_color_name is None and resolved_color_hex is None:
            resolved_color_name = self.current_color_name
            resolved_color_hex = self.current_color_hex
        elif resolved_color_name is None:
            resolved_color_name = self.favorite_color_name_for_hex(resolved_color_hex)

        return ToolbarState(
            active_tool=active_tool,
            color_name=resolved_color_name,
            color_hex=resolved_color_hex,
            draw_mode=active_tool,
            draw_cursor_active=bool(active_tool),
        )

    def current_toolbar_state(self):
        if self.active_tool in constants.VECTOR_COLOR_TOOLS:
            return self._build_toolbar_state(self.active_tool, color_name=self.color_name_for_tool(self.active_tool))
        return self._build_toolbar_state(self.active_tool, color_name=self.current_color_name)

    def select_tool(self, tool):
        self.active_tool = None if self.active_tool == tool else tool
        return self.current_toolbar_state()

    def clear_active_tool(self):
        self.active_tool = None

    def is_editing_selection(self, has_selected_vectors):
        return has_selected_vectors and self.active_tool is None

    def select_color(self, color_name, has_selected_vectors):
        color_hex = self.color_hex(color_name)
        if self.is_editing_selection(has_selected_vectors):
            return self._build_toolbar_state(None, color_name=color_name, color_hex=color_hex)

        self.remember_color(color_name)
        return self.current_toolbar_state()

    def handle_selection_context(self, context, color=None):
        if context == "editing":
            self.clear_active_tool()
            return self._build_toolbar_state(None, color_hex=color)

        return self.current_toolbar_state()

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
