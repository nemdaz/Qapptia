import os
import sys
import winsound
import ctypes
import platform
import time
import keyboard
import subprocess
from ctypes import wintypes
from PIL import ImageDraw, Image, ImageFilter
from core import config
from core.logger import logger

def get_resource_path(relative_path):
    """ Obtiene la ruta absoluta a un recurso, compatible con PyInstaller y desarrollo. """
    try:
        # PyInstaller crea una carpeta temporal y guarda la ruta en _MEIPASS
        base_path = sys._MEIPASS
    except Exception:
        base_path = os.path.abspath(".")
    return os.path.join(base_path, relative_path)

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

# Caché global para datos de audio (evita latencia de disco)
_AUDIO_CACHE = {}

def play_beep_async():
    """Reproduce el sonido de obturador profesional con precarga y telemetría."""
    timestamp = time.strftime('%H:%M:%S')
    logger.trace(f"[AUDIO] Solicitud de reproducción a las {timestamp}")
    global _AUDIO_CACHE
    try:
        if sys.platform == "win32":
            sound_key = "shutter"
            
            if sound_key not in _AUDIO_CACHE:
                sound_path = get_resource_path(os.path.join("core", "assets", "sounds", "shutter_a.wav"))
                logger.debug(f"[AUDIO] Verificando archivo local: {sound_path}")
                if os.path.exists(sound_path):
                    _AUDIO_CACHE[sound_key] = sound_path
                    logger.debug("[AUDIO] Ruta de audio referenciada exitosamente.")
                else:
                    logger.error("[AUDIO] ERROR: Archivo no encontrado. Ejecutando Beep de emergencia.")
                    winsound.Beep(1500, 150)
                    return

            # Reproducción instantánea desde archivo caché por OS
            winsound.PlaySound(_AUDIO_CACHE[sound_key], winsound.SND_FILENAME | winsound.SND_ASYNC)
            logger.debug("[AUDIO] Comando PlaySound enviado al OS.")
            
        elif sys.platform == "linux":
            print('\a', end='', flush=True)
    except Exception as e:
        logger.error(f"[AUDIO] Error al reproducir audio: {e}")

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

def get_dpi_scaling():
    """Despachador de factor de escala DPI según el Sistema Operativo."""
    os_name = platform.system().lower()
    if "windows" in os_name:
        return _get_dpi_win()
    elif "linux" in os_name:
        return _get_dpi_linux()
    return 1.0

def _get_dpi_win():
    """Cálculo de escala DPI para Windows usando GDI32."""
    try:
        user32 = ctypes.windll.user32
        gdi32 = ctypes.windll.gdi32
        logical_width = user32.GetSystemMetrics(0) # SM_CXSCREEN
        hdc = user32.GetDC(0)
        physical_width = gdi32.GetDeviceCaps(hdc, 118) # DESKTOPHORZRES
        user32.ReleaseDC(0, hdc)
        return physical_width / logical_width if logical_width > 0 else 1.0
    except:
        return 1.0

def _get_dpi_linux():
    """Intento de detección de escala DPI en Linux (Wayland/X11)."""
    try:
        # Prioridad a variable de entorno de GNOME/GTK
        if 'GDK_SCALE' in os.environ:
            return float(os.environ['GDK_SCALE'])
        # Fallback a xrandr si está disponible
        res = subprocess.check_output(['xrandr', '--current'], stderr=subprocess.STDOUT).decode()
        if ' connected primary' in res:
             # Lógica simplificada: comparar resoluciones si es necesario
             pass
    except:
        pass
    return 1.0

def _get_fallback_cursor(scale):
    """Genera un cursor de alta calidad con Antialiasing (Super-sampling)."""
    # Puntos base proporcionales de Windows estándar
    base_points = [(0, 0), (0, 16), (4, 12), (7, 19), (9, 18), (6, 11), (11, 11)]
    
    # Factor de super-muestreo (4x) para suavizar bordes
    ss = 4
    canvas_scale = scale * ss
    
    # Tamaño del lienzo temporal
    w, h = int(16 * canvas_scale), int(24 * canvas_scale)
    temp_img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(temp_img)
    
    # Dibujar polígono escalado en resolución 4x
    scaled_points = [(p[0] * canvas_scale, p[1] * canvas_scale) for p in base_points]
    d.polygon(scaled_points, fill="white", outline="black")
    
    # Reducir con LANCZOS (filtro de alta calidad para suavizado)
    final_w, final_h = int(w / ss), int(h / ss)
    cursor_img = temp_img.resize((final_w, final_h), Image.LANCZOS)
    
    return cursor_img, (0, 0)

def _get_real_cursor_win():
    """Captura el icono real del cursor en Windows usando APIs nativas."""
    class POINT(ctypes.Structure):
        _fields_ = [("x", wintypes.LONG), ("y", wintypes.LONG)]
        
    class CURSORINFO(ctypes.Structure):
        _fields_ = [("cbSize", wintypes.DWORD),
                    ("flags", wintypes.DWORD),
                    ("hCursor", wintypes.HANDLE),
                    ("ptScreenPos", POINT)]
                    
    class ICONINFO(ctypes.Structure):
        _fields_ = [("fIcon", wintypes.BOOL),
                    ("xHotspot", wintypes.DWORD),
                    ("yHotspot", wintypes.DWORD),
                    ("hbmMask", wintypes.HANDLE),
                    ("hbmColor", wintypes.HANDLE)]
                    
    class BITMAPINFOHEADER(ctypes.Structure):
        _fields_ = [("biSize", wintypes.DWORD), ("biWidth", wintypes.LONG), ("biHeight", wintypes.LONG),
                    ("biPlanes", wintypes.WORD), ("biBitCount", wintypes.WORD), ("biCompression", wintypes.DWORD),
                    ("biSizeImage", wintypes.DWORD), ("biXPelsPerMeter", wintypes.LONG), ("biYPelsPerMeter", wintypes.LONG),
                    ("biClrUsed", wintypes.DWORD), ("biClrImportant", wintypes.DWORD)]

    class BITMAPINFO(ctypes.Structure):
        _fields_ = [("bmiHeader", BITMAPINFOHEADER), ("bmiColors", wintypes.DWORD * 3)]

    try:
        u32 = ctypes.windll.user32
        g32 = ctypes.windll.gdi32
        
        u32.GetCursorInfo.argtypes = [ctypes.POINTER(CURSORINFO)]
        u32.GetCursorInfo.restype = wintypes.BOOL
        u32.GetIconInfo.argtypes = [wintypes.HANDLE, ctypes.POINTER(ICONINFO)]
        u32.GetIconInfo.restype = wintypes.BOOL
        u32.GetDC.argtypes = [wintypes.HWND]
        u32.GetDC.restype = wintypes.HDC
        u32.ReleaseDC.argtypes = [wintypes.HWND, wintypes.HDC]
        u32.ReleaseDC.restype = ctypes.c_int
        
        g32.CreateCompatibleDC.argtypes = [wintypes.HDC]
        g32.CreateCompatibleDC.restype = wintypes.HDC
        g32.DeleteDC.argtypes = [wintypes.HDC]
        g32.DeleteDC.restype = wintypes.BOOL
        g32.DeleteObject.argtypes = [wintypes.HANDLE]
        g32.DeleteObject.restype = wintypes.BOOL
        g32.CreateCompatibleBitmap.argtypes = [wintypes.HDC, ctypes.c_int, ctypes.c_int]
        g32.CreateCompatibleBitmap.restype = wintypes.HBITMAP
        g32.SelectObject.argtypes = [wintypes.HDC, wintypes.HANDLE]
        g32.SelectObject.restype = wintypes.HANDLE
        u32.DrawIconEx.argtypes = [wintypes.HDC, ctypes.c_int, ctypes.c_int, wintypes.HANDLE, 
                                   ctypes.c_int, ctypes.c_int, ctypes.c_uint, wintypes.HBRUSH, ctypes.c_uint]
        u32.DrawIconEx.restype = wintypes.BOOL
        g32.GetDIBits.argtypes = [wintypes.HDC, wintypes.HBITMAP, ctypes.c_uint, ctypes.c_uint, 
                                  wintypes.LPVOID, ctypes.POINTER(BITMAPINFO), ctypes.c_uint]
        g32.GetDIBits.restype = ctypes.c_int

        ci = CURSORINFO()
        ci.cbSize = ctypes.sizeof(CURSORINFO)
        if not u32.GetCursorInfo(ctypes.byref(ci)) or ci.flags != 1: 
            return None, (0, 0)
            
        ii = ICONINFO()
        if not u32.GetIconInfo(ci.hCursor, ctypes.byref(ii)): 
            return None, (0, 0)
            
        hotspot = (ii.xHotspot, ii.yHotspot)
        
        # Obtener tamaño real del cursor (High DPI aware)
        cw = u32.GetSystemMetrics(13) # SM_CXCURSOR
        ch = u32.GetSystemMetrics(14) # SM_CYCURSOR
        if cw == 0: cw = 32
        if ch == 0: ch = 32
        
        # Crear contexto de memoria para dibujar el icono
        hscr = u32.GetDC(0)
        hmem = g32.CreateCompatibleDC(hscr)
        hbmp = g32.CreateCompatibleBitmap(hscr, cw, ch)
        old_bmp = g32.SelectObject(hmem, hbmp)
        
        # Dibujar el icono real (flecha, mano, etc.) en nuestro bitmap
        # DI_NORMAL = 0x0003
        u32.DrawIconEx(hmem, 0, 0, ci.hCursor, cw, ch, 0, 0, 0x0003)
        
        # Extraer los píxeles del bitmap (DIB)
        bmi = BITMAPINFO()
        bmi.bmiHeader.biSize = ctypes.sizeof(BITMAPINFOHEADER)
        bmi.bmiHeader.biWidth = cw
        bmi.bmiHeader.biHeight = -ch # Negativo para orden Top-Down
        bmi.bmiHeader.biPlanes = 1
        bmi.bmiHeader.biBitCount = 32
        bmi.bmiHeader.biCompression = 0 # BI_RGB
        
        buffer = ctypes.create_string_buffer(cw * ch * 4)
        g32.GetDIBits(hmem, hbmp, 0, ch, buffer, ctypes.byref(bmi), 0)
        
        # Limpieza de recursos GDI (IMPORTANTE)
        if ii.hbmColor: g32.DeleteObject(ii.hbmColor)
        if ii.hbmMask: g32.DeleteObject(ii.hbmMask)
        g32.SelectObject(hmem, old_bmp)
        g32.DeleteObject(hbmp)
        g32.DeleteDC(hmem)
        u32.ReleaseDC(0, hscr)
        
        # Convertir a imagen PIL (BGRA -> RGBA) y corregir alpha premultiplicado.
        img = Image.frombuffer("RGBA", (cw, ch), buffer, "raw", "BGRA", 0, 1).copy()
        pixels = img.load()
        for y in range(ch):
            for x in range(cw):
                r, g, b, a = pixels[x, y]
                if a == 0:
                    pixels[x, y] = (0, 0, 0, 0)
                    continue
                if a < 255:
                    r = min(255, int((r * 255) / a))
                    g = min(255, int((g * 255) / a))
                    b = min(255, int((b * 255) / a))
                pixels[x, y] = (r, g, b, a)
        return img, hotspot
    except Exception as e:
        logger.error(f"Error capturando cursor nativo: {e}")
        return None, (0, 0)

def _get_real_cursor_linux():
    """Intento de capturar cursor real en Linux (X11/Wayland)."""
    # TODO: Implementar usando XFixes o revisando el cursor activo en /usr/share/icons
    # Por ahora devolvemos None para usar el Fallback Pro de alta calidad
    return None, (0, 0)

def get_current_cursor(scale):
    """Obtiene el cursor actual (real o fallback) de forma unificada y lista para usar."""
    cursor_img = None
    hotspot = (0, 0)
    os_name = platform.system().lower()
    
    # 1. Intentar obtener el cursor real del sistema según el SO
    if "windows" in os_name:
        cursor_img, hotspot = _get_real_cursor_win()
    elif "linux" in os_name:
        cursor_img, hotspot = _get_real_cursor_linux()
        
    # 2. Si falla el real (o no está soportado), usar Fallback Pro con Antialiasing
    if cursor_img is None:
        cursor_img, hotspot = _get_fallback_cursor(scale)
        
    return cursor_img, hotspot

def set_dpi_awareness():
    """Configura la consciencia de DPI de forma agnóstica al SO."""
    if sys.platform == "win32":
        try:
            ctypes.windll.shcore.SetProcessDpiAwareness(2) # PROCESS_PER_MONITOR_DPI_AWARE
        except:
            try:
                ctypes.windll.user32.SetProcessDPIAware()
            except:
                pass
    elif sys.platform == "linux":
        # En Linux, la escala suele gestionarse vía variables de entorno (GDK_SCALE)
        # o a nivel de toolkit (Qt/GTK), no requiere llamada global de proceso.
        pass
    elif sys.platform == "darwin":
        # macOS gestiona DPI (Retina) de forma nativa y transparente para el proceso.
        pass

def get_monitor_at_cursor():
    """Obtiene dimensiones (x, y, w, h) del monitor bajo el cursor."""
    if sys.platform == "win32":
        user32 = ctypes.windll.user32
        pt = wintypes.POINT()
        user32.GetCursorPos(ctypes.byref(pt))
        hMonitor = user32.MonitorFromPoint(pt, 1) # MONITOR_DEFAULTTONEAREST
        
        class MONITORINFO(ctypes.Structure):
            _fields_ = [("cbSize", wintypes.DWORD), ("rcMonitor", wintypes.RECT),
                        ("rcWork", wintypes.RECT), ("dwFlags", wintypes.DWORD)]
        
        mi = MONITORINFO()
        mi.cbSize = ctypes.sizeof(MONITORINFO)
        user32.GetMonitorInfoW(hMonitor, ctypes.byref(mi))
        r = mi.rcMonitor
        return r.left, r.top, r.right - r.left, r.bottom - r.top
    
    elif sys.platform == "linux":
        # TODO: Implementar usando xrandr o librerías específicas de X11/Wayland
        return 0, 0, 1920, 1080
    
    elif sys.platform == "darwin":
        # TODO: Implementar usando AppKit (NSScreen)
        return 0, 0, 1920, 1080
        
    return 0, 0, 1920, 1080 # Fallback absoluto

def get_virtual_screen_origin():
    """Obtiene el origen (x, y) del escritorio virtual completo."""
    if sys.platform == "win32":
        user32 = ctypes.windll.user32
        # SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77
        return user32.GetSystemMetrics(76), user32.GetSystemMetrics(77)
    elif sys.platform == "linux":
        # TODO: Implementar para X11/Wayland viewports
        return 0, 0
    elif sys.platform == "darwin":
        # Apple usa un sistema de coordenadas donde el origen suele ser el monitor principal
        return 0, 0
    return 0, 0

def draw_mouse_overlay(screen_image, mouse_x, mouse_y, highlight=False, cursor_data=None, highlight_style=None):
    """Dibuja el cursor sobre la captura. Permite pasar cursor_data=(img, hotspot) pre-capturado."""
    try:
        scale = get_dpi_scaling()
        mx, my = int(mouse_x), int(mouse_y)

        if cursor_data:
            cursor_img, hotspot = cursor_data
        else:
            cursor_img, hotspot = get_current_cursor(scale)

        hx, hy = hotspot

        if highlight:
            style = highlight_style or {}
            radius = int(style.get("radius", 24) * scale)
            fill = style.get("fill", (255, 220, 0, 92))
            supersample = max(1, int(style.get("supersample", 4)))
            halo_size = (radius * 2) + 2

            overlay = Image.new("RGBA", screen_image.size, (0, 0, 0, 0))
            halo_image = Image.new("RGBA", (halo_size * supersample, halo_size * supersample), (0, 0, 0, 0))
            halo_draw = ImageDraw.Draw(halo_image)
            halo_draw.ellipse(
                (0, 0, (halo_size * supersample) - 1, (halo_size * supersample) - 1),
                fill=fill,
            )
            halo_image = halo_image.resize((halo_size, halo_size), Image.LANCZOS)

            screen_image = screen_image.convert("RGBA")
            overlay.paste(halo_image, (mx - radius, my - radius), halo_image)
            screen_image = Image.alpha_composite(screen_image, overlay)

        screen_image = screen_image.convert("RGBA")
        screen_image.paste(cursor_img, (mx - int(hx * scale), my - int(hy * scale)), cursor_img)
        screen_image = screen_image.convert("RGB")

    except Exception as e:
        logger.error(f"Error en el dibujo del mouse: {e}")

    return screen_image

def register_hotkey(hotkey, callback, description=""):
    """Registra un atajo de teclado con validación de estado físico de modificadores."""
    if not hotkey: return False
    
    # Desglosamos el atajo para identificar teclas modificadoras (ctrl, shift, alt)
    parts = [p.strip().lower() for p in hotkey.split('+')]
    modifiers = [p for p in parts if p in ('ctrl', 'shift', 'alt', 'windows', 'cmd')]
    
    def safe_callback():
        """Validador de modificadores para prevenir activaciones accidentales."""
        time.sleep(0.05)
        if all(keyboard.is_pressed(m) for m in modifiers):
             logger.debug(f"[HOTKEY] Disparando '{description}' ({hotkey})")
             callback()
        else:
             logger.trace(f"[HOTKEY] Ignorada activacion de tecla solitaria: '{hotkey}'")
             
    try:
        # Usamos suppress=False para no interferir con otras apps (más estable)
        keyboard.add_hotkey(hotkey, safe_callback, suppress=False)
        desc_str = f"({description})" if description else ""
        logger.info(f"Atajo {desc_str} '{hotkey}' registrado (Protegido).")
        return True
    except Exception as e:
        logger.error(f"Error al registrar atajo {description} '{hotkey}': {e}")
        return False

