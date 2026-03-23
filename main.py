import pystray
import mouse
import keyboard
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
_editor_last_click = 0
_is_editor_launching = False

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

def launch_editor_process():
    """Lógica central para abrir el editor con protección de instancias multiples."""
    global _is_editor_launching
    
    # 1. Verificar si ya hay una instancia respondiendo (IPC)
    print("Verificando instancia del Editor...")
    if ipc.is_editor_running():
        print("Editor ya en ejecución. Enviada señal de despertar.")
        return

    # 2. Verificar si ya hay un proceso lanzándose (Protección de carrera)
    if _is_editor_launching:
        print("El editor ya se está iniciando, por favor espera...")
        return

    # 3. Lanzar nueva instancia
    print("Iniciando nueva instancia del Editor...")
    _is_editor_launching = True
    
    # Resetear el flag de lanzamiento después de un tiempo prudencial (5s)
    import threading
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
        print("Clic detectado en el icono, esperando segundo clic para abrir editor...")
        return

    _editor_last_click = 0
    launch_editor_process()

def open_editor_menu(icon, item=None):
    """Maneja el clic explícito en la opción del menú (abre con 1 clic)."""
    launch_editor_process()

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

def reload_hooks(icon=None, item=None):
    """Reinicia los hooks de teclado y ratón (Útil tras suspensión/hibernación)."""
    print("Reiniciando capturador (Limpiando hooks residuales)...")
    try:
        keyboard.unhook_all()
    except: pass
    try:
        mouse.unhook_all()
    except: pass
    
    config.load_config()
    setup_hotkeys(on_default_shortcut)
    try:
        mouse.hook(on_mouse_event)
    except: pass

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
        pystray.MenuItem('Abrir_oculto', open_editor_icon, default=True, visible=False),
        pystray.MenuItem('Capturar ahora', capture_full_menu),
        pystray.MenuItem(lambda text: 'Detener Flujo' if flow_manager.is_active else 'Iniciar Flujo', toggle_flow_menu),
        pystray.Menu.SEPARATOR,
        pystray.MenuItem('Reiniciar capturador', reload_hooks),
        pystray.Menu.SEPARATOR,
        pystray.MenuItem('Abrir Editor (Galería)', open_editor_menu),
        pystray.MenuItem('Configuración...', open_config),
        pystray.MenuItem('Salir', quit_app)
    )
    
    icon = pystray.Icon("screenshot_app", create_image(), f"{APP_NAME} v{VERSION}", menu)
    
    icon.run_detached(setup)
    
    last_time = time.time()
    while not should_exit:
        time.sleep(1)
        current_time = time.time()
        
        # Watchdog: Si pasa mucho tiempo en un solo 'sleep(1)', el PC fue suspendido
        jump = current_time - last_time
        if jump > 10.0:
            print(f"Salto de tiempo detectado ({jump:.1f}s). Probable suspensión. Reiniciando capturador...")
            reload_hooks()
            
        last_time = current_time
        
    # Limpieza final
    mouse.unhook_all()
    os._exit(0)

if __name__ == "__main__":
    main()
