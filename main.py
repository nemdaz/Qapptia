import sys
import os
import time
import subprocess
import threading

from core.logger import logger
from core import assets, config, ipc, utils
from core.constants import APP_NAME, VERSION
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

def create_image():
    return assets.create_app_icon_image(64)

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
    
    # 1. Verificar si ya hay una instancia respondiendo (IPC)
    logger.debug("Verificando instancia del Editor...")
    if ipc.is_editor_running():
        logger.debug("Editor ya en ejecución. Enviada señal de despertar.")
        return

    # 2. Verificar si ya hay un proceso lanzándose (Protección de carrera)
    if _is_editor_launching:
        logger.debug("El editor ya se está iniciando, por favor espera...")
        return

    # 3. Lanzar nueva instancia
    logger.info("Iniciando nueva instancia del Editor...")
    _is_editor_launching = True
    
    # Resetear el flag de lanzamiento después de un tiempo prudencial (5s)
    def reset_launching_flag():
        global _is_editor_launching
        _is_editor_launching = False
    threading.Timer(5.0, reset_launching_flag).start()

    if getattr(sys, 'frozen', False):
        subprocess.Popen([sys.executable, "--editor"])
    else:
        subprocess.Popen([sys.executable, sys.argv[0], "--editor"])

def open_editor_icon(icon, item=None):
    """Maneja el clic directo en el icono del tray (requiere doble clic)."""
    global _editor_last_click
    
    current_time = time.time()
    if current_time - _editor_last_click > 0.4:
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
    if getattr(sys, 'frozen', False):
        subprocess.Popen([sys.executable, "--config"])
    else:
        subprocess.Popen([sys.executable, sys.argv[0], "--config"])

def quit_app(icon, item=None):
    """Cierra la aplicación."""
    global should_exit
    logger.info("Saliendo...")
    ipc.request_editor_quit()
    icon.stop()
    should_exit = True

def reload_hooks(icon=None, item=None):
    """Reinicia la aplicación completa para restaurar hooks a bajo nivel."""
    global should_exit, should_restart
    logger.info("Reiniciando capturador completo (Recuperación de hilos OS)...")
    should_restart = True
    if icon:
        icon.stop()
    should_exit = True

def setup(icon):

    icon.visible = True
    if hasattr(icon, "notify"):
        try:
            icon.notify("La aplicación está activa en segundo plano.", APP_NAME)
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

    # Solo en el proceso principal (tray/captura).
    # En editor/config dejamos que Qt gestione DPI para evitar doble configuracion.
    _platform.dpi.set_process_dpi_awareness()

    global should_exit
    config.load_config()
    # Iniciar atajos de cada modo
    register_screen_hotkey()
    register_area_hotkey()
    register_flow_hotkey()
    
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
        time.sleep(1)
        current_time = time.time()
        
        # Watchdog: Si pasa mucho tiempo en un solo 'sleep(1)', el PC fue suspendido
        jump = current_time - last_time
        if jump > 10.0:
            logger.warning(f"Salto de tiempo detectado ({jump:.1f}s). Probable suspensión del OS. Reiniciando de raíz...")
            should_restart = True
            icon.stop()
            break
            
        last_time = current_time
        
    # Limpieza final
    try:
        _platform.input.unhook_all_mouse()
    except: pass
    
    if should_restart:
        logger.info("Ejecutando reinicio maestro de proceso...")
        time.sleep(0.5) # Dar tiempo a Windows para limpiar el icono de la bandeja
        if getattr(sys, 'frozen', False):
            os.execv(sys.executable, sys.argv)
        else:
            os.execv(sys.executable, [sys.executable] + sys.argv)
    else:
        os._exit(0)

if __name__ == "__main__":
    main()

