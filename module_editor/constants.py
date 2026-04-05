"""
Constantes para el módulo de edición (EditorApp).
"""
from core.version import APP_NAME

WINDOW_TITLE = f"{APP_NAME} Editor"
WINDOW_SIZE = "1100x700"
MIN_WIDTH = 800
MIN_HEIGHT = 500
SIDEBAR_WIDTH = 250
ICON_SIZE = (20, 20)

WINDOW_LAYOUT = {
    "window_size": WINDOW_SIZE,
    "min_width": MIN_WIDTH,
    "min_height": MIN_HEIGHT,
    "sidebar_width": SIDEBAR_WIDTH,
    "icon_size": ICON_SIZE,
}

ZOOM_CONFIG = {
    "min": 0.01,
    "max": 10.0,
    "step": 1.2,
    "presets": ["1%", "10%", "25%", "50%", "100%", "150%", "200%", "250%", "300%", "350%", "400%", "450%", "500%", "550%", "600%"],
}

UI_COLORS = {
    "bg_dark": "#2b2b2b",
    "canvas_bg": "#1a1a1a",
    "highlight": "#3a3a3a",
    "active_tool": "#1a73e8",
    "text_default": ("gray10", "gray90"),
    "tooltip_bg": "#2b2b2b",
    "tooltip_fg": "#ffffff",
}

# Colores Favoritos (Presets)
FAVORITE_COLORS = {
    "green": "#00ff00",
    "red": "#ff0000",
    "blue": "#0078D7",
    "cyan": "#00B7C3",
    "yellow": "#F7EB0C",
    "orange": "#ffa500"
}
FAVORITE_COLOR_NAMES = {
    "green": "Verde",
    "red": "Rojo",
    "blue": "Azul",
    "cyan": "Celeste",
    "yellow": "Amarillo",
    "orange": "Naranja"
}
DEFAULT_FAV_COLOR = "green"

# Tooltips y Textos UI
TOOLTIPS = {
    "rotate": "Rotar imagen",
    "copy_file": "Copiar archivo",
    "copy_clip": "Copiar captura al portapapeles",
    "save": "Guardar cambios",
    "arrow": "Dibujar flecha (A)",
    "rect": "Dibujar rectángulo (R)",
    "highlighter": "Resaltador (H)",
    "refresh": "Recargar explorador",
    "color_prefix": "Color: ",
    "image_fit": "Ajustar imagen",
    "image_real_size": "Ajustar imagen a tamaño real",
}

TOAST_MESSAGES = {
    "open_error": "Error al abrir la imagen",
    "rotate_success": "Rotado {degrees} grados",
    "save_success": "Cambios guardados y aplicados a la imagen",
    "save_error": "Error al guardar los cambios",
    "image_copied": "Imagen copiada",
    "copy_file_missing": "No hay archivo para copiar",
    "file_copied": "Archivo copiado",
}

HIGHLIGHTER_ALPHA = 102

# Fuentes
FONT_FAMILY = "Arial"
FONT_BOLD = (FONT_FAMILY, 12, "bold")
FONT_NORMAL = (FONT_FAMILY, 12, "normal")
FONT_HEADER = (FONT_FAMILY, 14, "bold")

# Dimensiones Sidebar
SCROLL_RESERVE_WIDTH = 16
INDENT_SIZE = 15
BTN_HEIGHT_SMALL = 24
BTN_REFRESH_SIZE = 30
ICON_SIZE_SMALL = (16, 16)

# Tiempos
INITIAL_LOAD_DELAY_MS = 300
DEBOUNCE_DELAY_MS = 50

VECTOR_STYLE = {
    "default_color": "#00FF00",
    "stroke_width": 3,
    "grip_size": 6,
    "arrow_wing_len": 25,
    "export_scale": 4,
    "export_max_pixels": 36000000,
    "draw_min_distance": 8,
    "selection_tolerance": {
        "rect": 8,
        "arrow": 10,
        "highlighter": 2,
    },
}

COLOR_SWATCH_STYLE = {
    "icon_size": 22,
    "outer_padding": 1,
    "outer_ring_active": "#f5f7fa",
    "outer_ring_inactive": "#2f343b",
    "inner_ring_active": UI_COLORS["active_tool"],
    "inner_ring_inactive": "#555b63",
}
