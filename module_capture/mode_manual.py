import time
from module_capture.capture_screen import capture_screen
from core import config

def trigger_manual_capture():
    """Ejecuta una captura manual con el temporizador configurado."""
    timer = config.get("manual_timer")
    if timer > 0:
        print(f"Captura manual en {timer} segundos...")
        time.sleep(timer)
    else:
        print("Captura manual solicitada...")
    capture_screen()
