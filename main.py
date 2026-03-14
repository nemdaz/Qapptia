import keyboard
import pystray
import mouse
from PIL import ImageGrab, Image
import datetime
import os
import winsound

# Estado global del flujo
is_flow_active = False

# Genera un icono simple (un cuadrado de color sólido)
def create_image():
    # Genera imagen 64x64
    image = Image.new('RGB', (64, 64), color=(0, 128, 255))
    return image

def capture_screen():
    try:
        # Obtener ruta de descargas (en Windows suele ser %USERPROFILE%\Downloads)
        user_profile = os.environ.get('USERPROFILE')
        if not user_profile:
            print("No se encontró el directorio de usuario.")
            return

        downloads_path = os.path.join(user_profile, 'Downloads')
        if not os.path.exists(downloads_path):
             os.makedirs(downloads_path)
        
        # Generar nombre del archivo basado en fecha y hora
        timestamp = datetime.datetime.now().strftime("%Y-%m-%d_%H-%M-%S")
        filename = f"screenshot_{timestamp}.png"
        filepath = os.path.join(downloads_path, filename)

        # Capturar la pantalla completa
        screen = ImageGrab.grab()
        screen.save(filepath, 'PNG')
        
        # Emitir un sonido corto
        winsound.Beep(1500, 150)
        
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
            print("Clic detectado en modo flujo, capturando pantalla...")
            capture_screen()

def toggle_flow(icon, item):
    global is_flow_active
    is_flow_active = not is_flow_active
    estado = "activado" if is_flow_active else "desactivado"
    print(f"Modo flujo de captura {estado}.")
    # Refresca el icono/menú si es necesario dependiendo del SO

def capture_manual(icon, item):
    print("Captura manual solicitada...")
    capture_screen()

# Configuración del menú del System Tray
def quit_app(icon, item):
    icon.stop()
    if is_flow_active:
        mouse.unhook_all()
    print("Cerrando aplicación...")
    # os._exit es la forma segura de terminar cuando hay hilos en background
    os._exit(0)

def setup(icon):
    icon.visible = True
    print("Registrando atajo: ctrl+shift+k")
    keyboard.add_hotkey('ctrl+shift+k', on_shortcut)
    print("Registrando listener de ratón para modo flujo...")
    mouse.hook(on_click)

def main():
    # Setup icon
    menu = pystray.Menu(
        pystray.MenuItem('Capturar ahora (Manual)', capture_manual),
        pystray.MenuItem(lambda text: 'Detener Flujo' if is_flow_active else 'Iniciar Flujo', toggle_flow),
        pystray.MenuItem('Salir', quit_app)
    )
    
    icon = pystray.Icon("screenshot_app", create_image(), "Capturador de Pantalla", menu)
    
    print("Iniciando aplicación en la bandeja del sistema...")
    # Executa la app
    icon.run(setup)

if __name__ == "__main__":
    main()
