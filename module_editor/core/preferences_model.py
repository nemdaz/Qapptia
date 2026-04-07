from dataclasses import dataclass, field

from module_editor import constants


@dataclass
class EditorPreferences:
    expanded_folders: list[str] = field(default_factory=list)
    last_selected_file: str | None = None
    active_fav_color: str = constants.DEFAULT_FAV_COLOR

    def to_dict(self):
        return {
            "expanded_folders": list(self.expanded_folders),
            "last_selected_file": self.last_selected_file,
            "active_fav_color": self.active_fav_color,
        }

    @classmethod
    def from_dict(cls, payload):
        return cls(
            expanded_folders=list(payload.get("expanded_folders", [])),
            last_selected_file=payload.get("last_selected_file"),
            active_fav_color=payload.get("active_fav_color", constants.DEFAULT_FAV_COLOR),
        )