from PySide6.QtWidgets import QLabel
from PySide6.QtCore import QTimer, Qt


TOAST_STYLES = {
    "info": {
        "background": "#333333",
        "border": "#6b7280",
    },
    "success": {
        "background": "#1f4d2e",
        "border": "#4caf50",
    },
    "error": {
        "background": "#5a1f1f",
        "border": "#ff6b6b",
    },
}

class Toast(QLabel):
    def __init__(self, parent, message, duration=2000, kind="info"):
        super().__init__(message, parent)
        style = TOAST_STYLES.get(kind, TOAST_STYLES["info"])
        self.setStyleSheet(
            f"background-color: {style['background']}; color: white; border: 1px solid {style['border']};"
            "border-radius: 10px; padding: 10px 20px; font-weight: bold; font-size: 13px;"
        )
        self.setAlignment(Qt.AlignCenter)
        self.adjustSize()
        
        # Position at center of parent
        w, h = self.width(), self.height()
        pw, ph = parent.width(), parent.height()
        self.move((pw - w) // 2, (ph - h) // 2)
        
        self.raise_()
        self.show()
        QTimer.singleShot(duration, self.hide)

def show_toast(parent, message, duration=2000, kind="info"):
    Toast(parent, message, duration, kind)
