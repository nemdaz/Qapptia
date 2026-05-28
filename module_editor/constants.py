"""
Constantes para el módulo de edición (EditorApp).
"""
from core.version import APP_NAME

WINDOW_TITLE = f"{APP_NAME} Editor"
ANNOTATION_DIR = ".dibujo"
SIDEBAR_WIDTH = 250

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
    "orange": "#ffa500",
    "white": "#ffffff",
    "black": "#000000"
}
FAVORITE_COLOR_NAMES = {
    "green": "Verde",
    "red": "Rojo",
    "blue": "Azul",
    "cyan": "Celeste",
    "yellow": "Amarillo",
    "orange": "Naranja",
    "white": "Blanco",
    "black": "Negro"
}
DEFAULT_FAV_COLOR = "green"

# Tipos de herramientas de dibujo
TOOL_TYPE_LINE = "line"
TOOL_TYPE_ARROW = "arrow"
TOOL_TYPE_RECT = "rect"
TOOL_TYPE_HIGHLIGHTER = "highlighter"
TOOL_TYPE_TEXT = "text"

VECTOR_COLOR_TOOLS = (TOOL_TYPE_LINE, TOOL_TYPE_ARROW, TOOL_TYPE_RECT, TOOL_TYPE_HIGHLIGHTER, TOOL_TYPE_TEXT)

# Tooltips y Textos UI
TOOLTIPS = {
    "rotate": "Rotar imagen",
    "copy_file": "Copiar archivo",
    "copy_clip": "Copiar captura al portapapeles",
    "save": "Guardar cambios",
    "line": "Dibujar línea (L)",
    "arrow": "Dibujar flecha (A)",
    "rect": "Dibujar rectángulo (R)",
    "highlighter": "Resaltador (H)",
    "text": "Agregar texto (T)",
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

# Tiempos
INITIAL_LOAD_DELAY_MS = 300

VECTOR_STYLE = {
    "default_color": "#00FF00",
    "stroke_width": 3,
    "grip_size": 6,
    "arrow_wing_len": 25,
    "export_scale": 4,
    "export_max_pixels": 36000000,
    "draw_min_distance": 8,
    "selection_tolerance": {
        "line": 10,
        "rect": 8,
        "arrow": 10,
        "highlighter": 2,
        "text": 8,
    },
}

TEXT_STYLE = {
    "fallback_family": "Arial",
    "font_files": {
        "regular": "AtkinsonHyperlegible-Regular.ttf",
        "bold": "AtkinsonHyperlegible-Bold.ttf",
    },
    "placeholder": "Escribe aquí...",
    "font_default_px": 20,
    "font_min_px": 12,
    "font_max_px": 320,
    "line_spacing_ratio": 1.18,
    "shadow_dark_rgba": (0, 0, 0, 90),
    "shadow_light_rgba": (255, 255, 255, 72),
    "shadow_dark_offsets": ((-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1)),
    "shadow_light_offsets": ((-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1)),
    "selection_border": "#f5f7fa",
    "selection_border_width": 1,
    "selection_dash_pattern": [4, 3],
    "create_min_distance": 18,
    "min_box_width": 36,
    "min_box_height": 24,
}

COLOR_SWATCH_STYLE = {
    "icon_size": 22,
}
