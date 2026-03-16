import pystray
import mouse
from PIL import Image
import os
import time
import subprocess
import sys

from core import config
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

def toggle_flow_menu(icon, item):
    """Activa/Desactiva el modo flujo."""
    is_active = flow_manager.toggle()
    print(f"Modo flujo {'activado' if is_active else 'desactivado'}.")

def capture_manual_menu(icon, item):
    """Dispara una captura manual."""
    trigger_manual_capture()

def open_config(icon, item):
    print("Abriendo configuración...")
    script_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "module_capture", "gui.py")
    subprocess.Popen([sys.executable, script_path])
    
    # Recargar para aplicar cambios de hotkeys inmediatamente
    config.load_config()
    setup_hotkeys(on_default_shortcut)

def open_editor(icon, item):
    print("Abriendo editor...")
    script_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "module_editor", "editor.py")
    subprocess.Popen([sys.executable, script_path])

def quit_app(icon, item):
    global should_exit
    print("Saliendo...")
    icon.stop()
    should_exit = True

def setup(icon):
    icon.visible = True
    # El registro de hotkeys se movió al main para mayor estabilidad
    # Siempre escuchamos el mouse, FlowManager decide si actuar
    mouse.hook(on_mouse_event)

def main():
    global should_exit
    config.load_config()
    setup_hotkeys(on_default_shortcut)
    
    menu = pystray.Menu(
        pystray.MenuItem('Capturar ahora', capture_manual_menu),
        pystray.MenuItem(lambda text: 'Detener Flujo' if flow_manager.is_active else 'Iniciar Flujo', toggle_flow_menu),
        pystray.Menu.SEPARATOR,
        pystray.MenuItem('Abrir Editor (Galería)', open_editor),
        pystray.MenuItem('Configuración...', open_config),
        pystray.MenuItem('Salir', quit_app)
    )
    
    icon = pystray.Icon("screenshot_app", create_image(), "QA Screenshot", menu)
    
    icon.run_detached(setup)
    
    while not should_exit:
        time.sleep(1)
        
    # Limpieza final
    mouse.unhook_all()
    os._exit(0)

if __name__ == "__main__":
    main()
