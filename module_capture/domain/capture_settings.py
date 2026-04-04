from dataclasses import dataclass


@dataclass
class CaptureSettings:
    save_path: str
    filename_format: str
    image_quality: int
    subfolder_month: bool
    subfolder_day: bool
    subfolder_hour: bool
    show_mouse: bool
    highlight_mouse: bool
    manual_timer: int
    shortcut_screen: str
    shortcut_area: str
    shortcut_flow: str
    shortcut_flow_pause: str
    enable_scroll_capture: bool

    def to_config_payload(self):
        return {
            "save_path": self.save_path,
            "filename_format": self.filename_format,
            "image_quality": self.image_quality,
            "subfolder_month": self.subfolder_month,
            "subfolder_day": self.subfolder_day,
            "subfolder_hour": self.subfolder_hour,
            "show_mouse": self.show_mouse,
            "highlight_mouse": self.highlight_mouse,
            "manual_timer": self.manual_timer,
            "shortcut_screen": self.shortcut_screen,
            "shortcut_area": self.shortcut_area,
            "shortcut_flow": self.shortcut_flow,
            "shortcut_flow_pause": self.shortcut_flow_pause,
            "enable_scroll_capture": self.enable_scroll_capture,
        }
