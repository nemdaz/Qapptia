import os
import sys
import time

from PIL import Image, ImageQt
from PySide6.QtCore import Qt
from PySide6.QtGui import QColor, QImage, QPainter

from core import config
from core.constants import DEFAULT_CONFIG
from core.input_runtime import remember_hotkey_registration
from core.logger import logger
from core.platform import get_platform_services

_platform = get_platform_services()

# Cache global para datos de audio (evita latencia de disco)
_AUDIO_CACHE = {}


def get_resource_path(relative_path):
    """Obtiene ruta absoluta de recursos en desarrollo y PyInstaller."""
    try:
        base_path = sys._MEIPASS
    except Exception:
        base_path = os.path.abspath(".")
    return os.path.join(base_path, relative_path)


def get_save_directory(base_path, now):
    """Calcula la ruta de guardado final segun configuracion de subcarpetas."""
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
    """Reproduce sonido de obturador de forma asincrona."""
    timestamp = time.strftime("%H:%M:%S")
    logger.trace(f"[AUDIO] Solicitud de reproduccion a las {timestamp}")

    global _AUDIO_CACHE
    try:
        sound_key = "shutter"
        if sound_key not in _AUDIO_CACHE:
            sound_path = get_resource_path(os.path.join("core", "assets", "sounds", "shutter_a.wav"))
            logger.debug(f"[AUDIO] Verificando archivo local: {sound_path}")
            if os.path.exists(sound_path):
                _AUDIO_CACHE[sound_key] = sound_path
                logger.debug("[AUDIO] Ruta de audio referenciada exitosamente.")
            else:
                logger.error("[AUDIO] Archivo no encontrado. Ejecutando beep de emergencia.")
                _platform.desktop.play_beep(None)
                return

        _platform.desktop.play_beep(_AUDIO_CACHE[sound_key])
        logger.debug("[AUDIO] Comando de reproduccion enviado al sistema.")
    except Exception as exc:
        logger.error(f"[AUDIO] Error al reproducir audio: {exc}")


def parse_filename_format(base_format, now_datetime):
    """Devuelve el nombre final usando tokens amigables."""
    if not base_format:
        base_format = DEFAULT_CONFIG["filename_format"]

    format_str = (
        base_format.replace("YYYY", "%Y")
        .replace("MM", "%m")
        .replace("DD", "%d")
        .replace("HH", "%H")
        .replace("mm", "%M")
        .replace("SS", "%S")
    )
    return now_datetime.strftime(format_str) + ".png"


def get_dpi_scaling():
    """Obtiene factor de escala DPI desde la capa de plataforma."""
    return _platform.desktop.get_dpi_scaling()


def get_current_cursor(scale):
    """Obtiene cursor actual (real o fallback) y hotspot."""
    return _platform.desktop.get_current_cursor(scale)


def set_dpi_awareness():
    """Configura DPI awareness en el proceso actual."""
    _platform.dpi.set_process_dpi_awareness()


def get_monitor_at_cursor():
    """Obtiene dimensiones (x, y, w, h) del monitor bajo el cursor."""
    return _platform.desktop.get_monitor_at_cursor()


def get_virtual_screen_origin():
    """Obtiene el origen (x, y) del escritorio virtual completo."""
    return _platform.desktop.get_virtual_screen_origin()


def _build_mouse_halo_image(radius, fill):
    """Construye la imagen RGBA del halo del cursor usando el motor de pintura de Qt."""
    halo_size = (radius * 2) + 2
    qimage = QImage(halo_size, halo_size, QImage.Format_ARGB32_Premultiplied)
    qimage.fill(Qt.GlobalColor.transparent)

    painter = QPainter(qimage)
    painter.setRenderHint(QPainter.RenderHint.Antialiasing, True)
    painter.setPen(Qt.PenStyle.NoPen)
    painter.setBrush(QColor(*fill))
    painter.drawEllipse(0, 0, halo_size - 1, halo_size - 1)
    painter.end()

    return ImageQt.fromqimage(qimage).convert("RGBA")


def draw_mouse_overlay(screen_image, mouse_x, mouse_y, highlight=False, cursor_data=None, highlight_style=None):
    """Dibuja cursor sobre captura, con halo opcional."""
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

            overlay = Image.new("RGBA", screen_image.size, (0, 0, 0, 0))
            halo_image = _build_mouse_halo_image(radius, fill)

            screen_image = screen_image.convert("RGBA")
            overlay.paste(halo_image, (mx - radius, my - radius), halo_image)

            screen_image = Image.alpha_composite(screen_image, overlay)

        screen_image = screen_image.convert("RGBA")
        screen_image.paste(cursor_img, (mx - int(hx * scale), my - int(hy * scale)), cursor_img)
        screen_image = screen_image.convert("RGB")
    except Exception as exc:
        logger.error(f"Error en el dibujo del mouse: {exc}")

    return screen_image


def register_hotkey(hotkey, callback, description=""):
    """Registra atajo con validacion de modificadores activos."""
    if not hotkey:
        return False

    parts = [p.strip().lower() for p in hotkey.split("+")]
    modifiers = [p for p in parts if p in ("ctrl", "shift", "alt", "windows", "cmd")]

    def safe_callback():
        time.sleep(0.05)
        if all(_platform.input.is_key_pressed(m) for m in modifiers):
            logger.debug(f"[HOTKEY] Disparando '{description}' ({hotkey})")
            callback()
        else:
            logger.trace(f"[HOTKEY] Ignorada activacion de tecla solitaria: '{hotkey}'")

    try:
        hotkey_handle = _platform.input.add_hotkey(hotkey, safe_callback, suppress=False)
        remember_hotkey_registration(hotkey, hotkey_handle, description)
        desc_str = f"({description})" if description else ""
        logger.info(f"Atajo {desc_str} '{hotkey}' registrado (Protegido).")
        return True
    except Exception as exc:
        logger.error(f"Error al registrar atajo {description} '{hotkey}': {exc}")
        return False