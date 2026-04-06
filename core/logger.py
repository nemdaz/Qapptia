import sys
from loguru import logger

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
    "logs/app_{time:YYYY-MM-DD}.log",
    level="DEBUG" if is_dev else "INFO",
    rotation="10 MB",
    retention="7 days",
    encoding="utf-8",
    enqueue=True,
)

logger.level("SUCCESS", color="<green>")
