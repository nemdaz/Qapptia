import threading
import time

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
        threading.Thread(target=capture_screen, daemon=True).start()
    elif mode == "area":
        capture_area()
    elif mode == "flow":
        capture_flow()
    else:
        logger.warning(f"[DISPATCHER] Modo de captura desconocido: {mode}")


def capture_screen():
    timer = config.get("manual_timer")
    if timer > 0:
        logger.info(CAPTURE_MESSAGES["screen_capture_wait"].format(timer=timer))
        time.sleep(timer)
    else:
        logger.info(CAPTURE_MESSAGES["screen_capture_now"])
    fullscreen_capture_service.capture_fullscreen()


def capture_area():
    app = QApplication.instance()
    if app is None:
        app = QApplication([])

    if app.thread() == QThread.currentThread():
        logger.info(CAPTURE_MESSAGES["area_mode_start"])
        run_area_selector()
    else:
        QTimer.singleShot(0, app, capture_area_on_main_thread)


def capture_area_on_main_thread():
    logger.info(CAPTURE_MESSAGES["area_mode_start"])
    run_area_selector()


def capture_flow():
    flow_capture_service.toggle()
