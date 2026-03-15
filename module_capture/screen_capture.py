import os
import datetime
import threading
from PIL import ImageGrab
import mouse

from core import config
from core import utils

def capture_screen(play_sound=True, flow_session_path=None):
    try:
        now = datetime.datetime.now()
        # Usamos la carpeta del flujo si se envió, de lo contrario usamos la lógica normal
        if flow_session_path:
            downloads_path = flow_session_path
        else:
            base_path = os.path.expandvars(config.get("save_path"))
            
            subfolders = []
            if config.get("subfolder_month"):
                subfolders.append(now.strftime("%Y-%m"))
            if config.get("subfolder_day"):
                subfolders.append(now.strftime("%Y-%m-%d"))
            if config.get("subfolder_hour"):
                subfolders.append(now.strftime("%Y-%m-%d %H"))
                
            downloads_path = os.path.join(base_path, *subfolders) if subfolders else base_path
        
        # Parseando tokens amigables usando las utils
        formato_crudo = config.get("filename_format")
        filename = utils.parse_filename_format(formato_crudo, now)
        filepath = os.path.join(downloads_path, filename)

        # Capturar la pantalla completa
        screen = ImageGrab.grab()
        
        # Superponer Cursor si está habilitado mediante las utilidades aisladas
        if config.get("show_mouse"):
            try:
                # Obtener coordenadas del mouse
                x, y = mouse.get_position()
                hl = config.get("highlight_mouse")
                screen = utils.draw_mouse_overlay(screen, x, y, hl)
            except Exception as ptr_e:
                 print(f"Error despachando request de dibujo de mouse: {ptr_e}")
        
        # Validar existencia de descarga y guardar finalmente
        try:
            if not os.path.exists(downloads_path):
                 os.makedirs(downloads_path)
            screen.save(filepath, 'PNG', quality=config.get("image_quality"))
        except FileNotFoundError:
            # Reintento robusto si la carpeta fue borrada simultáneamente en media ejecución
            os.makedirs(downloads_path, exist_ok=True)
            screen.save(filepath, 'PNG', quality=config.get("image_quality"))
        
        # Emitir un sonido corto en un hilo separado mediante proxy de las utils
        if play_sound:
            threading.Thread(target=utils.play_beep_async, daemon=True).start()
        
        print(f"Pantalla capturada y guardada en {filepath}")
    except Exception as e:
        print(f"Error al capturar la pantalla: {e}")
