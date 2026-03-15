import keyboard
import pystray
import mouse
from PIL import Image
import datetime
import os
import time
import subprocess
import sys

from core import config
from module_capture.screen_capture import capture_screen

# Estado global del flujo
is_flow_active = False
should_exit = False
current_hotkey_hook = None
current_flow_folder_path = None

# Genera un icono simple (un cuadrado de color sólido)
def create_image():
    image = Image.new('RGB', (64, 64), color=(0, 128, 255))
    return image

# Ejecuta la captura cuando se presiona la combinación (Modo Atajo)
def on_shortcut():
    print("Atajo presionado...")
    capture_screen()

# Listener para clics del mouse (Modo Flujo)
def on_click(event):
    if type(event) == mouse.ButtonEvent and event.event_type == 'down' and event.button == 'left':
        if is_flow_active:
            pause_key = config.get("flow_pause_key")
            if pause_key:
                try:
                    # Parsear atajo de pausa (ej: "ctrl+shift")
                    keys = [k.strip() for k in pause_key.split('+')]
                    paused = all(keyboard.is_pressed(k) for k in keys if k)
                    if paused:
                        print("Captura pausada por combinación de pausa.")
                        return
                except Exception as e:
                    print(f"Error evaluando tecla de pausa: {e}")

            print("Clic detectado en modo flujo, capturando pantalla silenciosamente...")
            capture_screen(play_sound=False, flow_session_path=current_flow_folder_path)

def toggle_flow(icon, item):
    global is_flow_active, current_flow_folder_path
    is_flow_active = not is_flow_active
    estado = "activado" if is_flow_active else "desactivado"
    print(f"Modo flujo de captura {estado}.")
    
    if is_flow_active:
        base_path = os.path.expandvars(config.get("save_path"))
        now = datetime.datetime.now()
        folder_name = f"{now.strftime('%Y-%m-%d %H%M%S')} Flujo"
        
        subfolders = []
        if config.get("subfolder_month"):
            subfolders.append(now.strftime("%Y-%m"))
        if config.get("subfolder_day"):
            subfolders.append(now.strftime("%Y-%m-%d"))
        if config.get("subfolder_hour"):
            subfolders.append(now.strftime("%Y-%m-%d %H"))
            
        parent_path = os.path.join(base_path, *subfolders) if subfolders else base_path
        current_flow_folder_path = os.path.join(parent_path, folder_name)
        
        if not os.path.exists(current_flow_folder_path):
            os.makedirs(current_flow_folder_path, exist_ok=True)
    else:
        current_flow_folder_path = None

def capture_manual(icon, item):
    timer = config.get("manual_timer")
    if timer > 0:
        print(f"Captura manual en {timer} segundos...")
        time.sleep(timer)
    else:
        print("Captura manual solicitada...")
    capture_screen()

# INVOCACIÓN MULTIPROCESO DE GUI
def open_config(icon, item):
    print("Iniciando Configuración en un subproceso aislado...")
    script_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "module_capture", "gui.py")
    subprocess.Popen([sys.executable, script_path])
    
    # Recargamos la config local automáticamente cada 5 segundos si está abierta
    # Solo para mantener sincronizados los atajos
    config.load_config()
    setup_hotkeys()

# Configuración del menú del System Tray
def quit_app(icon, item):
    global should_exit
    icon.stop()
    if is_flow_active:
        mouse.unhook_all()
    print("Cerrando aplicación...")
    should_exit = True

def setup_hotkeys():
    global current_hotkey_hook
    if current_hotkey_hook:
        try:
            keyboard.remove_hotkey(current_hotkey_hook)
        except:
            pass
    
    shortcut = config.get("shortcut_key")
    if shortcut:
        try:
            current_hotkey_hook = keyboard.add_hotkey(shortcut, on_shortcut)
            print(f"Atajo registrado: {shortcut}")
        except Exception as e:
            print(f"Error registrando atajo: {e}")

def setup(icon):
    icon.visible = True
    setup_hotkeys()
    print("Registrando listener de ratón para modo flujo...")
    mouse.hook(on_click)

def main():
    global should_exit
    
    # Refresh config at boot
    config.load_config()
    
    menu = pystray.Menu(
        pystray.MenuItem('Capturar ahora (Manual)', capture_manual),
        pystray.MenuItem(lambda text: 'Detener Flujo' if is_flow_active else 'Iniciar Flujo', toggle_flow),
        pystray.MenuItem('Configuración...', open_config),
        pystray.MenuItem('Salir', quit_app)
    )
    
    icon = pystray.Icon("screenshot_app", create_image(), "Capturador de Pantalla", menu)
    
    print("Iniciando aplicación en la bandeja del sistema...")
    icon.run_detached(setup)
    
    # Loop de vida
    while not should_exit:
        time.sleep(1)
        
    os._exit(0)

if __name__ == "__main__":
    main()
