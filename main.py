import keyboard
import pystray
import mouse
from PIL import ImageGrab, Image
import datetime
import os
import winsound
import time
import config
import threading

# Estado global del flujo
is_flow_active = False
should_open_gui = False
should_exit = False
current_hotkey_hook = None

# Genera un icono simple (un cuadrado de color sólido)
def create_image():
    image = Image.new('RGB', (64, 64), color=(0, 128, 255))
    return image

def play_beep():
    try:
        winsound.Beep(1500, 150)
    except:
        pass

def capture_screen(play_sound=True):
    try:
        base_path = os.path.expandvars(config.get("save_path"))
        
        now = datetime.datetime.now()
        subfolders = []
        if config.get("subfolder_month"):
            subfolders.append(now.strftime("%Y-%m"))
        if config.get("subfolder_day"):
            subfolders.append(now.strftime("%Y-%m-%d"))
        if config.get("subfolder_hour"):
            subfolders.append(now.strftime("%Y-%m-%d %H"))
            
        downloads_path = os.path.join(base_path, *subfolders) if subfolders else base_path
        
        if not os.path.exists(downloads_path):
             os.makedirs(downloads_path)
        
        formato_crudo = config.get("filename_format")
        if not formato_crudo:
            formato_crudo = "Screenshot_YYYYMMDD_HHmmSS"
            
        # Parseando tokens amigables
        format_str = formato_crudo.replace("YYYY", "%Y").replace("MM", "%m").replace("DD", "%d").replace("HH", "%H").replace("mm", "%M").replace("SS", "%S")
        
        filename = now.strftime(format_str) + ".png"
        filepath = os.path.join(downloads_path, filename)

        # Capturar la pantalla completa
        screen = ImageGrab.grab()
        screen.save(filepath, 'PNG', quality=config.get("image_quality"))
        
        # Emitir un sonido corto en un hilo separado para evitar bloqueos
        if play_sound:
            threading.Thread(target=play_beep, daemon=True).start()
        
        print(f"Pantalla capturada y guardada en {filepath}")
    except Exception as e:
        print(f"Error al capturar la pantalla: {e}")

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
            capture_screen(play_sound=False)

def toggle_flow(icon, item):
    global is_flow_active
    is_flow_active = not is_flow_active
    estado = "activado" if is_flow_active else "desactivado"
    print(f"Modo flujo de captura {estado}.")
    # pystray actualiza el texto con el lambda en Windows automáticamente

def capture_manual(icon, item):
    timer = config.get("manual_timer")
    if timer > 0:
        print(f"Captura manual en {timer} segundos...")
        time.sleep(timer)
    else:
        print("Captura manual solicitada...")
    capture_screen()

def open_config(icon, item):
    global should_open_gui
    should_open_gui = True

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

def reload_config():
    config.load_config()
    setup_hotkeys()

def main():
    global should_open_gui, should_exit
    
    menu = pystray.Menu(
        pystray.MenuItem('Capturar ahora (Manual)', capture_manual),
        pystray.MenuItem(lambda text: 'Detener Flujo' if is_flow_active else 'Iniciar Flujo', toggle_flow),
        pystray.MenuItem('Configuración...', open_config),
        pystray.MenuItem('Salir', quit_app)
    )
    
    icon = pystray.Icon("screenshot_app", create_image(), "Capturador de Pantalla", menu)
    
    print("Iniciando aplicación en la bandeja del sistema...")
    # Ejecutamos el icono de manera independiente para liberar el hilo principal y correr tkinter aquí
    icon.run_detached(setup)
    
    import gui
    
    # Bucle principal para manejar ventanas GUI (Tkinter requiere el hilo principal)
    while not should_exit:
        if should_open_gui:
            should_open_gui = False
            gui.run_gui(on_close_callback=reload_config)
            
        time.sleep(0.5)
        
    os._exit(0)

if __name__ == "__main__":
    main()
