import keyboard
from core import config
from module_capture.screen_capture import capture_screen

def setup_hotkeys(on_shortcut_callback):
    """Configura los atajos de teclado globales."""
    keyboard.unhook_all()
    
    hotkey = config.get("shortcut_key")
    if hotkey:
        try:
            keyboard.add_hotkey(hotkey, on_shortcut_callback)
            print(f"Atajo global registrado: {hotkey}")
        except Exception as e:
            print(f"Error al registrar atajo {hotkey}: {e}")

def on_default_shortcut():
    """Acción por defecto para el atajo de teclado."""
    print("Atajo presionado...")
    capture_screen()
