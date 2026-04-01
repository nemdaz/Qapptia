import time
from core.logger import logger
from core import config, utils
from module_capture.capture_screen import capture_screen

def setup():
    """Configura el disparador del Modo Pantalla."""
    hotkey = config.get("shortcut_screen")
    utils.register_hotkey(hotkey, trigger_screen_capture, "Modo Pantalla")

def trigger_screen_capture():
    """Ejecuta la lógica de negocio del Modo Pantalla (Timer + Captura)."""
    config.load_config()
    timer = config.get("manual_timer")
    if timer > 0:
        logger.info(f"Modo Pantalla: Captura en {timer} segundos...")
        time.sleep(timer)
    else:
        logger.info("Modo Pantalla: Captura inmediata solicitada...")
    
    capture_screen()
