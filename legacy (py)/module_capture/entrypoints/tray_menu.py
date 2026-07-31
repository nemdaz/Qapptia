import subprocess
import sys
import threading
import time

from core import config, constants as core_constants, ipc
from core.logger import logger
from core.platform import get_platform_services
from module_capture import constants
from module_capture.capture_dispatcher import request_capture
from module_capture.application.flow_capture_service import flow_capture_service
from module_capture.entrypoints.suspend_watcher import on_mouse_event

_platform = get_platform_services()

_tray_icon = None
_pending_existing_instance_notification = False
_pending_tray_icon_refresh = False
_editor_last_click = 0
_is_editor_launching = False
_restart_requested = False


def setup_tray_icon(tray_icon_instance):
    global _tray_icon, _pending_existing_instance_notification

    _tray_icon = tray_icon_instance
    tray_icon_instance.visible = True

    _try_notify(tray_icon_instance, "La aplicacion esta activa en segundo plano.")

    if _pending_existing_instance_notification:
        _try_notify(tray_icon_instance, "La aplicacion ya esta activa en segundo plano.")
        _pending_existing_instance_notification = False

    _platform.input.hook_mouse(on_mouse_event)


def build_tray_menu():
    return _platform.tray.menu(
        _platform.tray.menu_item('Abrir_oculto', _open_editor_icon, default=True, visible=False),
        _platform.tray.menu_item(lambda item: _capture_label('Capturar pantalla', 'shortcut_screen'), start_screen_capture),
        _platform.tray.menu_item(lambda item: _capture_label('Capturar área', 'shortcut_area'), start_area_capture),
        # _platform.tray.menu_item(lambda text: 'Capturar flujo (Detener)' if flow_capture_service.is_active else 'Capturar flujo (Iniciar)', start_flow_capture),
        _platform.tray.menu_separator(),
        _platform.tray.menu_item('Editor', _open_editor_menu),
        _platform.tray.menu_item('Configuración', _open_config),
        _platform.tray.menu_separator(),
        _platform.tray.menu_item('Reiniciar', _reload_hooks),
        _platform.tray.menu_item('Salir', _quit_app),
    )


def notify_existing_background_instance():
    global _pending_existing_instance_notification

    if _try_notify(_tray_icon, "La aplicacion ya esta activa en segundo plano."):
        return
    _pending_existing_instance_notification = True


def request_tray_icon_refresh():
    global _pending_tray_icon_refresh
    _pending_tray_icon_refresh = True


def process_pending_tray_refresh():
    global _pending_tray_icon_refresh

    if not _pending_tray_icon_refresh:
        return
    _pending_tray_icon_refresh = False

    if hasattr(_tray_icon, "update_menu"):
        try:
            _tray_icon.update_menu()
        except Exception as exc:
            logger.debug(f"No se pudo refrescar menu tray icon: {exc}")


def _capture_label(title, shortcut_key):
    config.load_config()
    hotkey = (config.get(shortcut_key) or "").strip().upper()
    return f"{title} ({hotkey})" if hotkey else title


def start_screen_capture(icon, item=None):
    request_capture(constants.CAPTURE_MODE_SCREEN, source=constants.CAPTURE_SOURCE_TRAY)


def start_area_capture(icon, item=None):
    request_capture(constants.CAPTURE_MODE_AREA, source=constants.CAPTURE_SOURCE_TRAY)


def start_flow_capture(icon, item=None):
    request_capture(constants.CAPTURE_MODE_FLOW, source=constants.CAPTURE_SOURCE_TRAY)


def _open_editor_icon(icon, item=None):
    global _editor_last_click

    current_time = time.time()
    if current_time - _editor_last_click > core_constants.RUNTIME_CONFIG["editor_double_click_seconds"]:
        _editor_last_click = current_time
        logger.debug("Clic detectado en el icono, esperando segundo clic para abrir editor...")
        return

    _editor_last_click = 0
    _launch_editor_process()


def _open_editor_menu(icon, item=None):
    _launch_editor_process()


def _launch_editor_process():
    global _is_editor_launching

    if ipc.request_wake_up(ipc.CHANNEL_EDITOR):
        logger.debug("Editor ya en ejecucion. Enviada senal de despertar.")
        return

    if _is_editor_launching:
        logger.debug("El editor ya se esta iniciando, por favor espera...")
        return

    logger.info("Iniciando nueva instancia del Editor...")
    _is_editor_launching = True

    def reset_launching_flag():
        global _is_editor_launching
        _is_editor_launching = False

    threading.Timer(core_constants.RUNTIME_CONFIG["editor_launch_guard_seconds"], reset_launching_flag).start()

    if getattr(sys, 'frozen', False):
        subprocess.Popen([sys.executable, "--editor"])
    else:
        subprocess.Popen([sys.executable, sys.argv[0], "--editor"])


def _open_config(icon, item=None):
    logger.info("Abriendo configuracion...")
    if ipc.request_wake_up(ipc.CHANNEL_CONFIG):
        logger.debug("Configuracion ya en ejecucion. Enviada senal de activacion.")
        return
    if getattr(sys, 'frozen', False):
        subprocess.Popen([sys.executable, "--config"])
    else:
        subprocess.Popen([sys.executable, sys.argv[0], "--config"])


def _quit_app(icon, item=None):
    logger.info("Saliendo...")
    ipc.request_quit(ipc.CHANNEL_EDITOR)
    ipc.request_quit(ipc.CHANNEL_CONFIG)
    icon.stop()
    from PySide6.QtWidgets import QApplication
    qt_app = QApplication.instance()
    if qt_app:
        qt_app.quit()


def _reload_hooks(icon=None, item=None):
    global _restart_requested
    logger.info("Reiniciando capturador completo por solicitud manual desde el menu...")
    _restart_requested = True
    icon = icon or _tray_icon
    if icon:
        icon.stop()
    from PySide6.QtWidgets import QApplication
    qt_app = QApplication.instance()
    if qt_app:
        qt_app.quit()


def is_restart_requested():
    return _restart_requested


def get_tray_icon():
    return _tray_icon


def _try_notify(icon, message):
    if icon and hasattr(icon, "notify"):
        try:
            icon.notify(message, core_constants.APP_NAME)
            return True
        except Exception:
            pass
    return False
