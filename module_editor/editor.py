import sys
import os
from PySide6.QtWidgets import QApplication
from core.logger import logger
from module_editor.ui.main_window import MainWindow

def _configure_crash_logging():
    import datetime
    import faulthandler

    os.makedirs("logs", exist_ok=True)
    crash_log = os.path.join("logs", f"editor_crash_{datetime.datetime.now().strftime('%Y%m%d_%H%M%S')}.log")
    faulthandler.enable(file=open(crash_log, "w", encoding="utf-8"), all_threads=True)

def _install_exception_hook():
    import traceback

    def _handle_exception(exc_type, exc_value, exc_tb):
        if issubclass(exc_type, KeyboardInterrupt):
            return
        logger.error(f"Editor crash: {''.join(traceback.format_exception(exc_type, exc_value, exc_tb))}")

    sys.excepthook = _handle_exception

def run_editor():
    _configure_crash_logging()
    _install_exception_hook()

    app = QApplication.instance() or QApplication(sys.argv)
    app.setStyle("Fusion")
    
    window = MainWindow()
    window.show()
    
    logger.info("Editor iniciado")
    sys.exit(app.exec())

if __name__ == "__main__":
    run_editor()
