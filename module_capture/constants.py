from core.constants import DEFAULT_CONFIG


DEFAULT_FILENAME_FORMAT = DEFAULT_CONFIG["filename_format"]


WINDOW_TEXT = {
    "title": "Configuracion de Capturador",
    "tabs": {
        "general": "General",
        "captures": "Capturas",
    },
    "buttons": {
        "browse": "Examinar",
        "save_close": "Guardar y Cerrar",
        "help": "?",
    },
    "labels": {
        "save_path": "Ruta de guardado:",
        "filename_format": "Formato de nombre:",
        "shortcut": "Atajo:",
        "timer": "Timer:",
        "pause": "Pausa:",
    },
    "groups": {
        "subfolders": "Organizar en subcarpetas",
        "cursor": "Cursor",
        "screen_mode": "Modo Pantalla",
        "area_mode": "Modo Area",
        "flow_mode": "Modo Flujo"
    },
    "checkboxes": {
        "subfolder_month": "Por mes (YYYY-MM)",
        "subfolder_day": "Por dia (YYYY-MM-DD)",
        "subfolder_hour": "Por hora (YYYY-MM-DD HH)",
        "show_mouse": "Capturar",
        "highlight_mouse": "Resaltar (Halo)",
        "enable_scroll_capture": "Habilitar Captura de Scroll Inteligente",
    },
    "placeholders": {
        "filename_format": DEFAULT_FILENAME_FORMAT,
        "shortcut": "Presiona teclas...",
    },
    "format_help": {
        "title": "Ayuda de Formato",
        "body": "\n".join(
            [
                "Usa los siguientes valores para formatear la fecha:",
                "",
                "YYYY = Ano (4 digitos)",
                "MM = Mes (2 digitos)",
                "DD = Dia (2 digitos)",
                "HH = Horas",
                "mm = Minutos",
                "SS = Segundos",
            ]
        ),
    },
}

WINDOW_LAYOUT = {
    "width": 640,
    "height": 560,
    "margin": 20,
    "spacing": 16,
    "help_button_width": 32,
    "timer_suffix": " s",
}

CAPTURE_DEFAULTS = {
    "filename_format": DEFAULT_FILENAME_FORMAT,
    "manual_timer": {
        "min": 0,
        "max": 999,
    },
}

AREA_SELECTOR_STYLE = {
    "min_selection_size": 5,
    "crosshair_color": "#00d9ff",
    "stroke_width": 1,
    "overlay_rgba": (0, 0, 0, 110),
    "monitor_poll_interval_ms": 100,
}

CURSOR_HIGHLIGHT_STYLE = {
    "radius": 22,
    "fill": (255, 255, 30, 200),
    "supersample": 4,
}

CAPTURE_MESSAGES = {
    "screen_capture_wait": "Modo Pantalla: Captura en {timer} segundos...",
    "screen_capture_now": "Modo Pantalla: Captura inmediata solicitada...",
    "screen_capture_success": "Captura guardada: {path}",
    "screen_capture_error": "Error captura: {error}",
    "screen_mouse_error": "Error dibujo mouse: {error}",
    "area_mode_start": "Modo Area: Activando selector de pantalla...",
    "area_crop_error": "Error al recortar area: {error}",
    "area_save_success": "Area capturada: {path}",
    "area_save_error": "Error al guardar captura de area: {error}",
    "capture_user_error": "Ocurrio un error durante la captura. Revisa el log de la aplicacion para mayor detalle.",
}

FLOW_MESSAGES = {
    "session_suffix": " Flujo",
    "session_started": "Sesion de flujo iniciada en: {path}",
    "manual_scroll_start": "Inicio de Scroll Manual en X relativa={x}",
    "manual_scroll_end": "Fin de Scroll Manual.",
    "manual_scroll_omitted": "Fin de Scroll omitido por redundancia.",
    "auto_capture": "--- Captura automatica ({reason}) ---",
    "reasons": {
        "click": "Clic",
        "manual_scroll_start": "Inicio Scroll",
        "manual_scroll_end": "Fin Scroll",
        "wheel_start": "Inicio Rueda",
        "wheel_pause": "Pausa Rueda",
        "manual_scroll_pause": "Pausa Scroll",
        "slow_scroll_cadence": "Cadencia Scroll Lento",
    },
}

HOTKEY_DESCRIPTIONS = {
    "screen": "Modo Pantalla",
    "area": "Modo Area",
    "flow": "Modo Flujo (Toggle)",
}
