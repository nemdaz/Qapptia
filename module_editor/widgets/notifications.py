from PySide6.QtWidgets import QLabel
from PySide6.QtCore import QTimer, Qt

class Toast(QLabel):
    def __init__(self, parent, message, duration=2000):
        super().__init__(message, parent)
        self.setStyleSheet(
            "background-color: #333333; color: white; border: 1px solid gray50;"
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

def show_toast(parent, message, duration=2000):
    Toast(parent, message, duration)
