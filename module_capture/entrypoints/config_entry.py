from PySide6.QtWidgets import QApplication

from core import assets, ipc
from core.platform import get_platform_services
from core.theme import apply_dark_theme

from module_capture.ui.config_window import CaptureConfigWindow

_platform = get_platform_services()


def run_config_window(on_close_callback=None):
    config_instance_guard = _platform.process.acquire_single_instance(ipc.CHANNEL_CONFIG)
    if config_instance_guard is None:
        ipc.request_wake_up(ipc.CHANNEL_CONFIG)
        return

    app = QApplication.instance()
    if app is None:
        app = QApplication([])

    apply_dark_theme(app)
    app_icon = assets.create_app_window_icon()
    app.setWindowIcon(app_icon)

    def _on_config_saved_and_closed():
        ipc.request_refresh_tray_icon(ipc.CHANNEL_APP)
        if on_close_callback:
            on_close_callback()

    window = CaptureConfigWindow(on_close_callback=_on_config_saved_and_closed)
    window.setWindowIcon(app_icon)

    ipc.start_server(ipc.CHANNEL_CONFIG, window.request_wake_up, window.request_close)
    window.exec()
