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

        # Monitor activo y origen del escritorio virtual
        mx, my, mw, mh = utils.get_monitor_at_cursor()
        vx, vy = utils.get_virtual_screen_origin()

        # Captura global y recorte al monitor detectado
        full_img = ImageGrab.grab(all_screens=True)
        ix, iy = mx - vx, my - vy
        screen = full_img.crop((ix, iy, ix + mw, iy + mh))
        
        if config.get("show_mouse"):
            try:
                # Posición física del mouse y escala
                tx, ty = mouse.get_position()
                scale = utils.get_dpi_scaling()
                cursor_data = utils.get_current_cursor(scale)
                
                # Posición relativa al monitor capturado
                rel_x, rel_y = (tx - mx) * scale, (ty - my) * scale
                
                hl = config.get("highlight_mouse")
                screen = utils.draw_mouse_overlay(screen, rel_x, rel_y, hl, cursor_data=cursor_data)
            except Exception as ptr_e:
                 print(f"Error dibujo mouse: {ptr_e}")
        
        # El guardado a disco (PNG) causa latencias de 100ms-400ms.
        screen.save(filepath, 'PNG', quality=config.get("image_quality"))
        
        if play_sound:
            utils.play_beep_async() # Llamada post-procesamiento
            
        print(f"Captura guardada: {filepath}")
    except Exception as e:
        print(f"Error captura: {e}")
