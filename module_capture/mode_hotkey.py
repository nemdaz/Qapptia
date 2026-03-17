import keyboard
from core import config
from module_capture.capture_screen import capture_screen
from module_capture.capture_area import trigger_area_capture

def setup_hotkeys(on_shortcut_callback):
    """Configura los atajos de teclado globales."""
    # Atajo configurable desde JSON (Pantalla Completa)
    hotkey = config.get("shortcut_key")
    if hotkey:
        try:
            keyboard.add_hotkey(hotkey, on_shortcut_callback)
            print(f"Atajo Pantalla Completa '{hotkey}' registrado.")
        except Exception as e:
            print(f"Error al registrar atajo {hotkey}: {e}")
            
    # Atajo Hardcoded para Captura de Área
    try:
        keyboard.add_hotkey("ctrl+shift+a", trigger_area_capture)
        print("Atajo Captura de Área 'ctrl+shift+a' registrado.")
    except Exception as e:
        print(f"Error al registrar atajo de área: {e}")

def on_default_shortcut():
    """Acción por defecto para el atajo de pantalla completa."""
    config.load_config()
    capture_screen()
