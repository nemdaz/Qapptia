import threading
import time

from PySide6.QtCore import QTimer, QThread
from PySide6.QtGui import QPixmap
from PySide6.QtWidgets import QApplication

from core import config
from core.logger import logger
from module_capture.application.fullscreen_capture_service import fullscreen_capture_service
from module_capture.application.flow_capture_service import flow_capture_service
from module_capture.constants import CAPTURE_MESSAGES, CAPTURE_MODE_SCREEN, CAPTURE_MODE_AREA, CAPTURE_MODE_FLOW
from module_capture.ui.area_selector import run_area_selector


def request_capture(mode, source="unknown"):
    logger.debug(f"[DISPATCHER] request_capture(mode={mode}, source={source})")
    config.load_config()

    if mode == CAPTURE_MODE_SCREEN:
        threading.Thread(target=capture_screen, daemon=True).start()
    elif mode == CAPTURE_MODE_AREA:
        capture_area()
    elif mode == CAPTURE_MODE_FLOW:
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

    output_path = fullscreen_capture_service.capture_fullscreen()
    if output_path and config.get("copy_to_clipboard_screen"):
        _copy_to_clipboard_on_main_thread(output_path)


def capture_area():
    app = QApplication.instance()
    if app is None:
        app = QApplication([])

    if app.thread() == QThread.currentThread():
        _capture_area_on_main_thread()
    else:
        QTimer.singleShot(0, app, _capture_area_on_main_thread)


def _capture_area_on_main_thread():
    logger.info(CAPTURE_MESSAGES["area_mode_start"])

    def _on_area_saved(output_path):
        if output_path and config.get("copy_to_clipboard_area"):
            _copy_to_clipboard_on_main_thread(output_path)

    run_area_selector(on_capture_callback=_on_area_saved)


def capture_flow():
    flow_capture_service.toggle()


def _copy_to_clipboard_on_main_thread(image_path):
    app = QApplication.instance()
    if app is None:
        logger.error("No hay QApplication para copiar al portapapeles")
        return
    if app.thread() == QThread.currentThread():
        _copy_to_clipboard(image_path)
    else:
        QTimer.singleShot(0, app, lambda: _copy_to_clipboard(image_path))


def _copy_to_clipboard(image_path):
    try:
        pixmap = QPixmap(image_path)
        if pixmap.isNull():
            logger.error(f"No se pudo cargar imagen para portapapeles: {image_path}")
            return
        clipboard = QApplication.clipboard()
        clipboard.setPixmap(pixmap)
        clipboard.setImage(pixmap.toImage())
        logger.debug(f"Imagen copiada al portapapeles: {image_path}")
    except Exception as exc:
        logger.error(f"Error copiando imagen al portapapeles: {exc}")
