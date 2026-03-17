"""
Constantes para el módulo de edición (EditorApp).
"""

WINDOW_TITLE = "QA Screenshot Editor"
WINDOW_SIZE = "1100x700"
MIN_WIDTH = 800
MIN_HEIGHT = 500
SIDEBAR_WIDTH = 250
ICON_SIZE = (20, 20)

# Zoom
ZOOM_MIN = 0.1
ZOOM_MAX = 5.0
ZOOM_STEP = 1.1

# Colores de UI
BG_COLOR_DARK = "#2b2b2b"
CANVAS_BG_COLOR = "#1a1a1a"
HIGHLIGHT_COLOR = "#3a3a3a"
ACTIVE_TOOL_COLOR = "#1a73e8"
TEXT_COLOR_DEFAULT = ("gray10", "gray90")
TOOLTIP_BG_COLOR = "#2b2b2b"
TOOLTIP_FG_COLOR = "#ffffff"

# Colores Favoritos (Presets)
FAVORITE_COLORS = {
    "green": "#00ff00",
    "red": "#ff0000",
    "lightblue": "#33BBFF",
    "orange": "#ffa500"
}
FAVORITE_COLOR_NAMES = {
    "green": "Verde",
    "red": "Rojo",
    "lightblue": "Azul claro",
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
    "refresh": "Recargar explorador",
    "color_prefix": "Color: "
}

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

# Vector Rendering
DEFAULT_VECTOR_COLOR = "#00FF00"
VECTOR_WIDTH = 3
GRIP_SIZE = 5
ARROW_WING_LEN = 25
