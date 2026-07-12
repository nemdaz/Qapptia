import sys
import os
import datetime
from loguru import logger


LOGS_DIR = "logs"


def _ensure_logs_dir():
    os.makedirs(LOGS_DIR, exist_ok=True)


def build_loguru_daily_path(prefix):
    _ensure_logs_dir()
    return os.path.join(LOGS_DIR, f"{prefix}_{{time:YYYY-MM-DD}}.log")


def build_daily_log_path(prefix, when=None):
    _ensure_logs_dir()
    date_value = when or datetime.datetime.now()
    return os.path.join(LOGS_DIR, f"{prefix}_{date_value.strftime('%Y-%m-%d')}.log")

# Eliminar handler por defecto
logger.remove()

# Detectar entorno: frozen (PyInstaller) = producción
is_dev = not getattr(sys, 'frozen', False)
console_sink = sys.stderr or sys.__stderr__

if console_sink is not None:
    if is_dev:
        logger.add(
            console_sink,
            level="DEBUG",
            format="<green>{time:HH:mm:ss}</green> | <level>{level:<8}</level> | <cyan>{name}</cyan> - {message}",
            diagnose=True,
            backtrace=True,
        )
    else:
        logger.add(
            console_sink,
            level="WARNING",
            format="{time:HH:mm:ss} | {level} | {message}",
        )

# Log rotativo en archivo (siempre activo)
logger.add(
    build_loguru_daily_path("app"),
    level="DEBUG" if is_dev else "INFO",
    rotation="10 MB",
    retention="7 days",
    encoding="utf-8",
    enqueue=True,
    backtrace=True,
    diagnose=is_dev,
)

logger.level("SUCCESS", color="<green>")
