import os
import datetime
import threading
from PIL import ImageGrab
import mouse
from core import config, utils

def capture_screen(play_sound=True, flow_session_path=None):
    """Realiza una captura de pantalla completa."""
    try:
        now = datetime.datetime.now()
        if flow_session_path:
            downloads_path = flow_session_path
        else:
            downloads_path = utils.get_save_directory(config.get("save_path"), now)
        
        filename = utils.parse_filename_format(config.get("filename_format"), now)
        filepath = os.path.join(downloads_path, filename)

        screen = ImageGrab.grab()
        
        if config.get("show_mouse"):
            try:
                x, y = mouse.get_position()
                hl = config.get("highlight_mouse")
                screen = utils.draw_mouse_overlay(screen, x, y, hl)
            except Exception as ptr_e:
                 print(f"Error dibujo mouse: {ptr_e}")
        
        screen.save(filepath, 'PNG', quality=config.get("image_quality"))
        
        if play_sound:
            threading.Thread(target=utils.play_beep_async, daemon=True).start()
        
        print(f"Captura guardada: {filepath}")
    except Exception as e:
        print(f"Error captura: {e}")
