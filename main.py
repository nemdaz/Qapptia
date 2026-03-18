import pystray
import mouse
from PIL import Image
import os
import time
import subprocess
import sys

from core import config, ipc
from core.constants import APP_NAME, VERSION
from module_capture.capture_screen import capture_screen
from module_capture.capture_area import trigger_area_capture
from module_capture.mode_manual import trigger_manual_capture
from module_capture.mode_hotkey import setup_hotkeys, on_default_shortcut
from module_capture.mode_flow import flow_manager

# Estado global
should_exit = False

def create_image():
    # Icono simple para la bandeja
    image = Image.new('RGB', (64, 64), color=(0, 128, 255))
    return image

def on_mouse_event(event):
    """Delega los eventos del ratón al FlowManager."""
    flow_manager.handle_mouse_event(event)

def toggle_flow_menu(icon, item=None):
    """Activa/Desactiva el modo flujo."""
    is_active = flow_manager.toggle()
    print(f"Modo flujo {'activado' if is_active else 'desactivado'}.")

def capture_full_menu(icon, item=None):
    """Captura pantalla completa desde el menú enviando a manual_capture (por el delay)."""
    config.load_config()
    trigger_manual_capture()

def capture_area_menu(icon, item=None):
    """Inicia captura de área desde el menú."""
    config.load_config()
    trigger_area_capture()

def open_editor(icon, item=None):
    """Abre el editor de capturas o lo trae al frente si ya está abierto."""
    print("Verificando instancia del Editor...")
    if ipc.is_editor_running():
        print("Editor ya en ejecución. Enviada señal de despertar.")
        return

    print("Iniciando nueva instancia del Editor...")
    if getattr(sys, 'frozen', False):
        subprocess.Popen([sys.executable, "--editor"])
    else:
        subprocess.Popen([sys.executable, sys.argv[0], "--editor"])

def open_config(icon, item=None):
    """Abre la ventana de configuración usando el propio ejecutable o script."""
    print("Abriendo configuración...")
    if getattr(sys, 'frozen', False):
        subprocess.Popen([sys.executable, "--config"])
    else:
        subprocess.Popen([sys.executable, sys.argv[0], "--config"])

def quit_app(icon, item=None):
    """Cierra la aplicación."""
    global should_exit
    print("Saliendo...")
    icon.stop()
    should_exit = True

def setup(icon):
    icon.visible = True
    # Siempre escuchamos el mouse, FlowManager decide si actuar
    mouse.hook(on_mouse_event)

def main():
    # Despachador para modo portable / PyInstaller
    if len(sys.argv) > 1:
        if sys.argv[1] == "--editor":
            from module_editor.editor import EditorApp
            app = EditorApp()
            app.mainloop()
            return
        elif sys.argv[1] == "--config":
            from module_capture.gui import run_gui
            run_gui()
            return

    global should_exit
    config.load_config()
    setup_hotkeys(on_default_shortcut)
    
    menu = pystray.Menu(
        pystray.MenuItem('Capturar ahora', capture_full_menu),
        pystray.MenuItem(lambda text: 'Detener Flujo' if flow_manager.is_active else 'Iniciar Flujo', toggle_flow_menu),
        pystray.Menu.SEPARATOR,
        pystray.MenuItem('Abrir Editor (Galería)', open_editor),
        pystray.MenuItem('Configuración...', open_config),
        pystray.MenuItem('Salir', quit_app)
    )
    
    icon = pystray.Icon("screenshot_app", create_image(), f"{APP_NAME} v{VERSION}", menu)
    
    icon.run_detached(setup)
    
    while not should_exit:
        time.sleep(1)
        
    # Limpieza final
    mouse.unhook_all()
    os._exit(0)

if __name__ == "__main__":
    main()
