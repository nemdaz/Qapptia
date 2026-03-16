"""
Constantes de configuración para el módulo de captura y sus sub-modos.
"""

# --- Smart Scroll Flow (Manual) ---
SCROLL_ZONE_WIDTH_RATIO = 0.94          # Margen del 6% derecho para detectar scrollbar
SCROLL_ZONE_WIDTH_PIXELS = 80          # Margen fijo en píxeles (fallback/complemento)

JITTER_THRESHOLD = 15                  # Píxeles por debajo de los cuales se considera temblor/reposo
SLOW_SCROLL_MAX_SPEED = 150            # Píxeles/500ms por debajo de los cuales se considera lectura
MIN_DISTANCE_BETWEEN_CAPTURES = 60     # Distancia mínima para nueva captura de pausa manual
MIN_DISTANCE_SLOW_SCROLL = 100         # Distancia mínima acumulada para captura por cadencia
SLOW_SCROLL_CADENCE_TIME = 2.5         # Segundos entre capturas durante scrolleo lento

VELOCITY_CHECK_INTERVAL = 0.5          # Intervalo de monitoreo de velocidad (segundos)

# --- Smart Scroll Flow (Wheel/Rueda) ---
WHEEL_IDLE_TIME = 1.0                  # Tiempo para considerar que inicia una nueva ráfaga (segundos)
WHEEL_SMART_PAUSE_DEBOUNCE = 0.8       # Tiempo de inactividad para tomar captura de pausa (segundos)
