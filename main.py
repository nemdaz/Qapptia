import os
import sys
import time

from PySide6.QtCore import QTimer
from PySide6.QtWidgets import QApplication

from core import assets, ipc
from core.constants import APP_NAME, VERSION, RUNTIME_CONFIG
from core.logger import logger
from core.platform import get_platform_services
from module_capture.entrypoints.hotkey_register import register_all_capture_hotkeys
from module_capture.entrypoints.tray_menu import (
    build_tray_menu,
    get_tray_icon,
    is_restart_requested,
    notify_existing_background_instance,
    process_pending_tray_refresh,
    request_tray_icon_refresh,
    setup_tray_icon,
)
from module_capture.entrypoints.suspend_watcher import check_suspend_jump
from module_capture.entrypoints.config_entry import run_config_window
from module_editor.editor import run_editor

_platform = get_platform_services()
_last_tick_time = 0


def main():
    global _last_tick_time

    if len(sys.argv) > 1:
        if sys.argv[1] == "--editor":
            run_editor()
            return
        elif sys.argv[1] == "--config":
            run_config_window()
            return

    app_instance_guard = _platform.process.acquire_single_instance(ipc.CHANNEL_APP)
    if app_instance_guard is None:
        if not ipc.request_wake_up(ipc.CHANNEL_APP):
            _platform.desktop.show_info_message(APP_NAME, "La aplicacion ya esta activa en segundo plano.")
        return

    ipc.start_server(
        ipc.CHANNEL_APP,
        notify_existing_background_instance,
        on_refresh_tray_icon_callback=request_tray_icon_refresh,
    )

    if not register_all_capture_hotkeys():
        logger.warning("Uno o mas atajos no pudieron registrarse en el arranque.")

    tray_icon = _platform.tray.icon(
        "screenshot_app",
        assets.create_app_tray_icon_image(32),
        f"{APP_NAME} v{VERSION}",
        build_tray_menu(),
    )
    tray_icon.run_detached(setup_tray_icon)

    qt_app = QApplication.instance() or QApplication(sys.argv)

    tick_timer = QTimer()
    tick_timer.timeout.connect(_main_tick)
    tick_timer.start(int(RUNTIME_CONFIG["main_loop_sleep_seconds"] * 1000))

    _last_tick_time = time.time()

    qt_app.exec()

    tick_timer.stop()
    try:
        _platform.input.unhook_all_mouse()
    except Exception:
        pass

    if should_restart():
        logger.info("Ejecutando reinicio maestro de proceso...")
        time.sleep(RUNTIME_CONFIG["restart_grace_period_seconds"])
        if getattr(sys, 'frozen', False):
            os.execv(sys.executable, sys.argv)
        else:
            os.execv(sys.executable, [sys.executable] + sys.argv)
    else:
        return 0


def should_restart():
    return is_restart_requested() or _suspend_restart_requested()


_suspend_restart = False


def _suspend_restart_requested():
    return _suspend_restart


def _main_tick():
    global _last_tick_time, _suspend_restart

    process_pending_tray_refresh()

    _last_tick_time, action = check_suspend_jump(_last_tick_time, get_tray_icon())
    if action == "restart":
        _suspend_restart = True
        qt_app = QApplication.instance()
        if qt_app:
            qt_app.quit()


if __name__ == "__main__":
    sys.exit(main() or 0)
