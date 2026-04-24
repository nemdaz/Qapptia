import threading

from PySide6.QtCore import QTimer, QThread
from PySide6.QtWidgets import QApplication

from core import config
from core.logger import logger
from module_capture.application.fullscreen_capture_service import fullscreen_capture_service
from module_capture.application.flow_capture_service import flow_capture_service
from module_capture.constants import CAPTURE_MESSAGES
from module_capture.ui.area_selector import run_area_selector


def request_capture(mode, source="unknown"):
    logger.debug(f"[DISPATCHER] request_capture(mode={mode}, source={source})")
    config.load_config()

    if mode == "screen":
        threading.Thread(target=fullscreen_capture_service.capture_with_timer, daemon=True).start()
    elif mode == "area":
        _dispatch_area_capture()
    elif mode == "flow":
        flow_capture_service.toggle()
    else:
        logger.warning(f"[DISPATCHER] Modo de captura desconocido: {mode}")


def _dispatch_area_capture():
    app = QApplication.instance()
    if app is None:
        app = QApplication([])

    if app.thread() == QThread.currentThread():
        logger.info(CAPTURE_MESSAGES["area_mode_start"])
        run_area_selector()
    else:
        QTimer.singleShot(0, app, _area_capture_on_main_thread)


def _area_capture_on_main_thread():
    logger.info(CAPTURE_MESSAGES["area_mode_start"])
    run_area_selector()
