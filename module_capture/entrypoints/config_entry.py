from PySide6.QtWidgets import QApplication

from module_capture.ui.config_window import CaptureConfigWindow


def run_config_window(on_close_callback=None):
    app = QApplication.instance()
    if app is None:
        app = QApplication([])

    window = CaptureConfigWindow(on_close_callback=on_close_callback)
    window.exec()
