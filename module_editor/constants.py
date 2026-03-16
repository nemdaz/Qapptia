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
ZOOM_STEP = 1.1 # Factor multiplicador por cada paso de rueda

# Apariencia y Colores
TOOLTIP_BG_COLOR = "#2b2b2b"
TOOLTIP_FG_COLOR = "#ffffff"
HIGHLIGHT_COLOR = "#3a3a3a" # Color para ítems seleccionados

# Tiempos y Delays
INITIAL_LOAD_DELAY_MS = 300
TOOLTIP_DELAY_MS = 500
DEBOUNCE_DELAY_MS = 50 # Para anti-flicker en redimensionamiento

# Configuraciones de Rendering
DEFAULT_VECTOR_COLOR = "#00FF00"
VECTOR_WIDTH = 3
GRIP_SIZE = 5
ARROW_WING_LEN = 25
