from PIL import ImageQt
from PySide6.QtGui import QIcon, QPixmap
from PySide6.QtWidgets import QApplication

from core import assets

from module_capture.ui.config_window import CaptureConfigWindow


def run_config_window(on_close_callback=None):
    app = QApplication.instance()
    if app is None:
        app = QApplication([])

    app_icon = QIcon(QPixmap.fromImage(ImageQt.ImageQt(assets.create_app_icon_image(64))))
    app.setWindowIcon(app_icon)

    window = CaptureConfigWindow(on_close_callback=on_close_callback)
    window.setWindowIcon(app_icon)
    window.exec()
