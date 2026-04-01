from core.logger import logger
from core import config, utils
from module_capture.capture_area import CaptureAreaUI

def setup():
    """Configura el disparador del Modo Area."""
    hotkey = config.get("shortcut_area")
    utils.register_hotkey(hotkey, trigger_area_capture, "Modo Area")

def trigger_area_capture(callback=None):
    """Inicia la lógica de negocio y la interfaz del Modo Area."""
    config.load_config()
    logger.info("Modo Area: Activando selector de pantalla...")
    app = CaptureAreaUI(on_capture_callback=callback)
    app.run()
