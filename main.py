import os
import subprocess
import sys
import threading
import time

from PySide6.QtCore import QTimer
from PySide6.QtWidgets import QApplication

from core.logger import logger
from core import assets, config, ipc
from core.constants import APP_NAME, VERSION, RUNTIME_CONFIG
from core.platform import get_platform_services
from module_capture.capture_dispatcher import request_capture
from module_capture.application.flow_capture_service import flow_capture_service
from module_capture.entrypoints.area_mode import register_area_hotkey
from module_capture.entrypoints.config_entry import run_config_window
from module_capture.entrypoints.flow_mode import register_flow_hotkey
from module_capture.entrypoints.screen_mode import register_screen_hotkey
from module_editor.editor import run_editor

_platform = get_platform_services()
_tray_icon = None
should_exit = False
should_restart = False
_editor_last_click = 0
_is_editor_launching = False
_pending_existing_instance_notification = False
_last_tick_time = 0


def _register_capture_hotkeys():
    config.load_config()
    screen_ok = register_screen_hotkey()
    area_ok = register_area_hotkey()
    flow_ok = register_flow_hotkey()
    return bool(screen_ok and area_ok and flow_ok)


def _restore_global_input_after_resume(icon=None):
    if _platform.input.requires_process_restart_after_resume():
        logger.info("El SO requiere reiniciar el proceso para recuperar los hooks globales tras reanudacion.")
    else:
        logger.info("Reintentando restaurar los hooks globales de entrada tras reanudacion del sistema...")

    recovered = _platform.input.restore_global_hooks_after_resume(
        register_hotkeys_callback=_register_capture_hotkeys,
        mouse_callback=on_mouse_event,
        max_attempts=RUNTIME_CONFIG["hook_recovery_max_attempts"],
        retry_delay_seconds=RUNTIME_CONFIG["hook_recovery_retry_delay_seconds"],
    )
    if recovered:
        logger.info("Hooks globales de entrada restaurados correctamente tras reanudacion.")
        if icon and hasattr(icon, "notify"):
            try:
                icon.notify("Hooks globales restaurados tras reanudacion.", APP_NAME)
            except Exception:
                pass
    else:
        if _platform.input.requires_process_restart_after_resume():
            logger.warning("Los hooks globales requieren reinicio completo del proceso tras reanudacion en este SO.")
        else:
            logger.error("No fue posible restaurar los hooks globales de entrada tras reanudacion.")
    return recovered


# ── Tray icon callbacks ──────────────────────────────────────────────────


def _tray_icon_capture_label(title, shortcut_key):
    config.load_config()
    hotkey = (config.get(shortcut_key) or "").strip().upper()
    if not hotkey:
        return title
    return f"{title} ({hotkey})"


def on_mouse_event(event):
    flow_capture_service.handle_mouse_event(event)


def _capture_screen(icon, item=None):
    request_capture("screen", source="tray")


def _capture_area(icon, item=None):
    request_capture("area", source="tray")


def _toggle_flow(icon, item=None):
    request_capture("flow", source="tray")


def _open_editor_icon(icon, item=None):
    global _editor_last_click

    current_time = time.time()
    if current_time - _editor_last_click > RUNTIME_CONFIG["editor_double_click_seconds"]:
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

    threading.Timer(RUNTIME_CONFIG["editor_launch_guard_seconds"], reset_launching_flag).start()

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
    global should_exit
    logger.info("Saliendo...")
    ipc.request_quit(ipc.CHANNEL_EDITOR)
    ipc.request_quit(ipc.CHANNEL_CONFIG)
    icon.stop()
    should_exit = True
    _quit_qt_app()


def _reload_hooks(icon=None, item=None):
    global should_exit, should_restart
    logger.info("Reiniciando capturador completo por solicitud manual desde el menu...")
    should_restart = True
    if icon:
        icon.stop()
    should_exit = True
    _quit_qt_app()


# ── IPC callbacks ─────────────────────────────────────────────────────────


def _notify_existing_background_instance():
    global _pending_existing_instance_notification

    message = "La aplicacion ya esta activa en segundo plano."
    if _tray_icon and hasattr(_tray_icon, "notify"):
        try:
            _tray_icon.notify(message, APP_NAME)
            return
        except Exception as exc:
            logger.debug(f"No se pudo notificar instancia existente: {exc}")

    _pending_existing_instance_notification = True


def _request_tray_icon_refresh():
    global _pending_tray_icon_refresh
    _pending_tray_icon_refresh = True


_pending_tray_icon_refresh = False


# ── Setup & main loop ─────────────────────────────────────────────────────


def _setup_tray_icon(tray_icon_instance):
    global _tray_icon, _pending_existing_instance_notification

    _tray_icon = tray_icon_instance
    tray_icon_instance.visible = True
    if hasattr(tray_icon_instance, "notify"):
        try:
            tray_icon_instance.notify("La aplicacion esta activa en segundo plano.", APP_NAME)
            if _pending_existing_instance_notification:
                tray_icon_instance.notify("La aplicacion ya esta activa en segundo plano.", APP_NAME)
                _pending_existing_instance_notification = False
        except Exception as exc:
            logger.debug(f"No se pudo mostrar notificacion de bandeja: {exc}")
    _platform.input.hook_mouse(on_mouse_event)


def _quit_qt_app():
    qt_app = QApplication.instance()
    if qt_app:
        qt_app.quit()


def _main_tick():
    global should_exit, should_restart, _pending_tray_icon_refresh, _last_tick_time

    if _pending_tray_icon_refresh:
        _pending_tray_icon_refresh = False
        if hasattr(_tray_icon, "update_menu"):
            try:
                _tray_icon.update_menu()
            except Exception as exc:
                logger.debug(f"No se pudo refrescar menu tray icon: {exc}")

    current_time = time.time()
    jump = current_time - _last_tick_time
    if jump > RUNTIME_CONFIG["suspend_jump_threshold_seconds"]:
        logger.warning(f"Salto de tiempo detectado ({jump:.1f}s). Probable suspension del OS.")
        if not _restore_global_input_after_resume(_tray_icon):
            if _platform.input.requires_process_restart_after_resume():
                logger.warning("Reiniciando el capturador para recuperar los hooks globales tras reanudacion...")
            else:
                logger.warning("No fue posible reactivar los hooks globales de entrada tras reanudacion. Reiniciando el capturador...")
            should_restart = True
            _quit_qt_app()
            return
    _last_tick_time = current_time


def _build_tray_menu():
    return _platform.tray.menu(
        _platform.tray.menu_item('Abrir_oculto', _open_editor_icon, default=True, visible=False),
        _platform.tray.menu_item(lambda item: _tray_icon_capture_label('Capturar pantalla', 'shortcut_screen'), _capture_screen),
        _platform.tray.menu_item(lambda item: _tray_icon_capture_label('Capturar area', 'shortcut_area'), _capture_area),
        # _platform.tray.menu_item(lambda text: 'Capturar flujo (Detener)' if flow_capture_service.is_active else 'Capturar flujo (Iniciar)', _toggle_flow),
        _platform.tray.menu_separator(),
        _platform.tray.menu_item('Editor', _open_editor_menu),
        _platform.tray.menu_item('Configuracion', _open_config),
        _platform.tray.menu_separator(),
        _platform.tray.menu_item('Reiniciar', _reload_hooks),
        _platform.tray.menu_item('Salir', _quit_app),
    )


def main():
    global should_exit, should_restart, _last_tick_time

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
        _notify_existing_background_instance,
        on_refresh_tray_icon_callback=_request_tray_icon_refresh,
    )

    if not _register_capture_hotkeys():
        logger.warning("Uno o mas atajos no pudieron registrarse en el arranque.")

    tray_icon = _platform.tray.icon(
        "screenshot_app",
        assets.create_app_tray_icon_image(32),
        f"{APP_NAME} v{VERSION}",
        _build_tray_menu(),
    )
    tray_icon.run_detached(_setup_tray_icon)

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

    if should_restart:
        logger.info("Ejecutando reinicio maestro de proceso...")
        time.sleep(RUNTIME_CONFIG["restart_grace_period_seconds"])
        if getattr(sys, 'frozen', False):
            os.execv(sys.executable, sys.argv)
        else:
            os.execv(sys.executable, [sys.executable] + sys.argv)
    else:
        return 0


if __name__ == "__main__":
    sys.exit(main() or 0)
