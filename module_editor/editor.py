import sys
import os
import atexit
from PIL import ImageQt
from PySide6.QtGui import QIcon, QPixmap
from PySide6.QtWidgets import QApplication
from PySide6.QtCore import QtMsgType, qInstallMessageHandler
from core.logger import logger, build_daily_log_path
from core import assets
from module_editor.ui.main_window import MainWindow

_crash_log_file = None
_crash_log_path = None


def _configure_crash_logging():
    import datetime
    import faulthandler

    global _crash_log_file, _crash_log_path

    now = datetime.datetime.now()
    _crash_log_path = build_daily_log_path("editor", now)
    _crash_log_file = open(_crash_log_path, "a", encoding="utf-8")
    _crash_log_file.write(
        f"\n--- Editor session start | pid={os.getpid()} | ts={now.isoformat()} ---\n"
    )
    _crash_log_file.flush()
    faulthandler.enable(file=_crash_log_file, all_threads=True)


def _cleanup_crash_logging():
    global _crash_log_file
    if _crash_log_file and not _crash_log_file.closed:
        _crash_log_file.flush()
        _crash_log_file.close()

def _install_exception_hook():
    import traceback

    def _handle_exception(exc_type, exc_value, exc_tb):
        if issubclass(exc_type, KeyboardInterrupt):
            return
        logger.error(f"Editor crash: {''.join(traceback.format_exception(exc_type, exc_value, exc_tb))}")

    sys.excepthook = _handle_exception


def _install_qt_message_handler():
    def _qt_handler(mode, _context, message):
        msg = message.strip()
        if not msg:
            return
        if "SetProcessDpiAwarenessContext() failed" in msg or "Qt's default DPI awareness context" in msg:
            return
        if mode == QtMsgType.QtFatalMsg or mode == QtMsgType.QtCriticalMsg:
            logger.error(f"Qt: {msg}")
            return
        if mode == QtMsgType.QtWarningMsg:
            logger.warning(f"Qt: {msg}")
            return
        logger.debug(f"Qt: {msg}")

    qInstallMessageHandler(_qt_handler)


def run_editor():
    _configure_crash_logging()
    _install_exception_hook()
    _install_qt_message_handler()
    atexit.register(_cleanup_crash_logging)

    app = QApplication.instance() or QApplication(sys.argv)
    app.setStyle("Fusion")
    app_icon = QIcon(QPixmap.fromImage(ImageQt.ImageQt(assets.create_app_icon_image(64))))
    app.setWindowIcon(app_icon)
    
    window = MainWindow()
    window.setWindowIcon(app_icon)
    window.show()
    
    logger.info(f"Editor iniciado (pid={os.getpid()})")
    sys.exit(app.exec())

if __name__ == "__main__":
    run_editor()
