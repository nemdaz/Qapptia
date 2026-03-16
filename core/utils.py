import os
import winsound
from PIL import ImageDraw, Image
from core import config

def get_save_directory(base_path, now):
    """Calcula la ruta de guardado final basada en la configuración de subcarpetas."""
    base_path = os.path.expandvars(base_path)
    subfolders = []
    if config.get("subfolder_month"):
        subfolders.append(now.strftime("%Y-%m"))
    if config.get("subfolder_day"):
        subfolders.append(now.strftime("%Y-%m-%d"))
    if config.get("subfolder_hour"):
        subfolders.append(now.strftime("%Y-%m-%d %H"))
        
    full_path = os.path.join(base_path, *subfolders) if subfolders else base_path
    if not os.path.exists(full_path):
        os.makedirs(full_path, exist_ok=True)
    return full_path

def play_beep_async():
    """Reproduce el beep de éxito de captura."""
    try:
        winsound.Beep(1500, 150)
    except:
        pass

def parse_filename_format(base_format, now_datetime):
    """Pule y devuelve el string final usando los tokens amigables."""
    if not base_format:
        base_format = "Screenshot_YYYYMMDD_HHmmSS"
        
    format_str = (base_format
                  .replace("YYYY", "%Y")
                  .replace("MM", "%m")
                  .replace("DD", "%d")
                  .replace("HH", "%H")
                  .replace("mm", "%M")
                  .replace("SS", "%S"))
                  
    return now_datetime.strftime(format_str) + ".png"

def draw_mouse_overlay(screen_image, mouse_x, mouse_y, highlight=False):
    """Dibuja el falso cursor sobre la imagen original de captura."""
    try:
        if highlight:
            # Capa transparente del tamaño de la captura
            overlay = Image.new('RGBA', screen_image.size, (0,0,0,0))
            d_ctx = ImageDraw.Draw(overlay)
            
            radio = 25
            halo_box = [mouse_x - radio, mouse_y - radio, mouse_x + radio, mouse_y + radio]
            # Halo amarillo translúcido
            d_ctx.ellipse(halo_box, fill=(255, 255, 0, 100))
            
            screen_image = screen_image.convert("RGBA")
            screen_image = Image.alpha_composite(screen_image, overlay)
            screen_image = screen_image.convert("RGB")
            
        # Puntero Base
        d = ImageDraw.Draw(screen_image)
        cursor_points = [
            (mouse_x, mouse_y), 
            (mouse_x, mouse_y + 17), 
            (mouse_x + 4, mouse_y + 13), 
            (mouse_x + 7, mouse_y + 20), 
            (mouse_x + 10, mouse_y + 19), 
            (mouse_x + 7, mouse_y + 12), 
            (mouse_x + 12, mouse_y + 12)
        ]
        d.polygon(cursor_points, fill="white", outline="black")
    except Exception as e:
        print(f"Error parseando el puntero del mouse helper: {e}")
        
    return screen_image
