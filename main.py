import os
import subprocess
import sys
import threading
import time

from core.logger import logger
from core import assets, config, ipc, utils
from core.constants import APP_NAME, VERSION, RUNTIME_CONFIG
from core.platform import get_platform_services
from module_capture.application.flow_capture_service import flow_capture_service
from module_capture.entrypoints.area_mode import register_area_hotkey, trigger_area_capture
from module_capture.entrypoints.config_entry import run_config_window
from module_capture.entrypoints.flow_mode import register_flow_hotkey
from module_capture.entrypoints.screen_mode import register_screen_hotkey, trigger_screen_capture
from module_editor.editor import run_editor

# Estado global
should_exit = False
should_restart = False
_editor_last_click = 0
_is_editor_launching = False
_platform = get_platform_services()
_tray_icon = None
_pending_existing_instance_notification = False


def _register_capture_hotkeys():
    config.load_config()
    screen_ok = register_screen_hotkey()
    area_ok = register_area_hotkey()
    flow_ok = register_flow_hotkey()
    return bool(screen_ok and area_ok and flow_ok)


def _restore_global_input_after_resume(icon=None):
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
        logger.error("No fue posible restaurar los hooks globales de entrada tras reanudacion.")
    return recovered


def _notify_existing_background_instance():
    global _pending_existing_instance_notification

    message = "La aplicación ya está activa en segundo plano."
    if _tray_icon and hasattr(_tray_icon, "notify"):
        try:
            _tray_icon.notify(message, APP_NAME)
            return
        except Exception as exc:
            logger.debug(f"No se pudo notificar instancia existente: {exc}")

    _pending_existing_instance_notification = True

def create_image():
    return assets.create_app_tray_icon_image(32)

def on_mouse_event(event):
    """Delega los eventos del ratón al FlowManager."""
    flow_capture_service.handle_mouse_event(event)

def toggle_flow_menu(icon, item=None):
    """Activa/Desactiva el modo flujo."""
    is_active = flow_capture_service.toggle()
    logger.info(f"Modo flujo {'activado' if is_active else 'desactivado'}.")

def capture_full_menu(icon, item=None):
    """Captura pantalla completa desde el menú."""
    config.load_config()
    trigger_screen_capture()

def capture_area_menu(icon, item=None):
    """Inicia captura de área desde el menú."""
    config.load_config()
    trigger_area_capture()

def launch_editor_process():
    """Lógica central para abrir el editor con protección de instancias multiples."""
    global _is_editor_launching

    if ipc.request_wake_up(ipc.CHANNEL_EDITOR):
        logger.debug("Editor ya en ejecución. Enviada señal de despertar.")
        return

    if _is_editor_launching:
        logger.debug("El editor ya se está iniciando, por favor espera...")
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

def open_editor_icon(icon, item=None):
    """Maneja el clic directo en el icono del tray (requiere doble clic)."""
    global _editor_last_click
    
    current_time = time.time()
    if current_time - _editor_last_click > RUNTIME_CONFIG["editor_double_click_seconds"]:
        _editor_last_click = current_time
        logger.debug("Clic detectado en el icono, esperando segundo clic para abrir editor...")
        return

    _editor_last_click = 0
    launch_editor_process()

def open_editor_menu(icon, item=None):
    """Maneja el clic explícito en la opción del menú (abre con 1 clic)."""
    launch_editor_process()

def open_config(icon, item=None):
    """Abre la ventana de configuración usando el propio ejecutable o script."""
    logger.info("Abriendo configuración...")
    if ipc.request_wake_up(ipc.CHANNEL_CONFIG):
        logger.debug("Configuración ya en ejecución. Enviada señal de activación.")
        return
    if getattr(sys, 'frozen', False):
        subprocess.Popen([sys.executable, "--config"])
    else:
        subprocess.Popen([sys.executable, sys.argv[0], "--config"])

def quit_app(icon, item=None):
    """Cierra la aplicación."""
    global should_exit
    logger.info("Saliendo...")
    ipc.request_quit(ipc.CHANNEL_EDITOR)
    ipc.request_quit(ipc.CHANNEL_CONFIG)
    icon.stop()
    should_exit = True

def reload_hooks(icon=None, item=None):
    """Reinicia manualmente el capturador completo desde el menú de bandeja."""
    global should_exit, should_restart
    logger.info("Reiniciando capturador completo por solicitud manual desde el menú...")
    should_restart = True
    if icon:
        icon.stop()
    should_exit = True

def setup(icon):
    global _tray_icon, _pending_existing_instance_notification

    _tray_icon = icon
    icon.visible = True
    if hasattr(icon, "notify"):
        try:
            icon.notify("La aplicación está activa en segundo plano.", APP_NAME)
            if _pending_existing_instance_notification:
                icon.notify("La aplicación ya está activa en segundo plano.", APP_NAME)
                _pending_existing_instance_notification = False
        except Exception as exc:
            logger.debug(f"No se pudo mostrar notificación de bandeja: {exc}")
    # Siempre escuchamos el mouse, FlowManager decide si actuar
    _platform.input.hook_mouse(on_mouse_event)

def main():
    global should_exit, should_restart
    # Despachador para modo portable / PyInstaller
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
            _platform.desktop.show_info_message(APP_NAME, "La aplicación ya está activa en segundo plano.")
        return

    ipc.start_server(ipc.CHANNEL_APP, _notify_existing_background_instance)

    # Solo en el proceso principal (tray/captura).
    # En editor/config dejamos que Qt gestione DPI para evitar doble configuracion.
    _platform.dpi.set_process_dpi_awareness()

    global should_exit
    if not _register_capture_hotkeys():
        logger.warning("Uno o mas atajos no pudieron registrarse en el arranque.")
    
    menu = _platform.tray.menu(
        _platform.tray.menu_item('Abrir_oculto', open_editor_icon, default=True, visible=False),
        _platform.tray.menu_item('Capturar pantalla', capture_full_menu),
        _platform.tray.menu_item(lambda text: 'Capturar flujo (Detener)' if flow_capture_service.is_active else 'Capturar flujo (Iniciar)', toggle_flow_menu),
        _platform.tray.menu_separator(),
        _platform.tray.menu_item('Editor', open_editor_menu),
        _platform.tray.menu_item('Configuración', open_config),
        _platform.tray.menu_separator(),
        _platform.tray.menu_item('Reiniciar', reload_hooks),
        _platform.tray.menu_item('Salir', quit_app)
    )
    
    icon = _platform.tray.icon("screenshot_app", create_image(), f"{APP_NAME} v{VERSION}", menu)
    
    icon.run_detached(setup)
    
    last_time = time.time()
    while not should_exit:
        time.sleep(RUNTIME_CONFIG["main_loop_sleep_seconds"])
        current_time = time.time()
        
        # Watchdog: Si pasa mucho tiempo en un solo 'sleep(1)', el PC fue suspendido
        jump = current_time - last_time
        if jump > RUNTIME_CONFIG["suspend_jump_threshold_seconds"]:
            logger.warning(f"Salto de tiempo detectado ({jump:.1f}s). Probable suspensión del OS.")
            if not _restore_global_input_after_resume(icon):
                logger.warning("No fue posible reactivar los hooks globales de entrada tras reanudacion. Reiniciando el capturador...")
                should_restart = True
                icon.stop()
                break
            
        last_time = current_time

    # Limpieza final
    try:
        _platform.input.unhook_all_mouse()
    except Exception:
        pass
    
    if should_restart:
        logger.info("Ejecutando reinicio maestro de proceso...")
        time.sleep(RUNTIME_CONFIG["restart_grace_period_seconds"]) # Dar tiempo al OS para limpiar el icono de la bandeja
        if getattr(sys, 'frozen', False):
            os.execv(sys.executable, sys.argv)
        else:
            os.execv(sys.executable, [sys.executable] + sys.argv)
    else:
        return 0

if __name__ == "__main__":
    sys.exit(main() or 0)

