from dataclasses import dataclass, field

from module_editor import constants


@dataclass
class EditorPreferences:
    expanded_folders: list[str] = field(default_factory=list)
    last_selected_file: str | None = None
    active_fav_color: str = constants.DEFAULT_FAV_COLOR
    tool_fav_colors: dict[str, str] = field(default_factory=dict)

    def __post_init__(self):
        normalized = {
            tool: self.tool_fav_colors.get(tool, self.active_fav_color)
            for tool in constants.VECTOR_COLOR_TOOLS
        }
        self.tool_fav_colors = normalized

    def to_dict(self):
        return {
            "expanded_folders": list(self.expanded_folders),
            "last_selected_file": self.last_selected_file,
            "active_fav_color": self.active_fav_color,
            "tool_fav_colors": dict(self.tool_fav_colors),
        }

    @classmethod
    def from_dict(cls, payload):
        return cls(
            expanded_folders=list(payload.get("expanded_folders", [])),
            last_selected_file=payload.get("last_selected_file"),
            active_fav_color=payload.get("active_fav_color", constants.DEFAULT_FAV_COLOR),
            tool_fav_colors=dict(payload.get("tool_fav_colors", {})),
        )