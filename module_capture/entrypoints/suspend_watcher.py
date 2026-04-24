import time

from core.logger import logger
from core.constants import APP_NAME, RUNTIME_CONFIG
from core.platform import get_platform_services
from module_capture.entrypoints.hotkey_register import register_all_capture_hotkeys

_platform = get_platform_services()


def restore_global_input_after_resume(icon=None):
    if _platform.input.requires_process_restart_after_resume():
        logger.info("El SO requiere reiniciar el proceso para recuperar los hooks globales tras reanudacion.")
    else:
        logger.info("Reintentando restaurar los hooks globales de entrada tras reanudacion del sistema...")

    recovered = _platform.input.restore_global_hooks_after_resume(
        register_hotkeys_callback=register_all_capture_hotkeys,
        mouse_callback=on_mouse_event,
        max_attempts=RUNTIME_CONFIG["hook_recovery_max_attempts"],
        retry_delay_seconds=RUNTIME_CONFIG["hook_recovery_retry_delay_seconds"],
    )

    if recovered:
        logger.info("Hooks globales de entrada restaurados correctamente tras reanudacion.")
        _try_notify(icon, "Hooks globales restaurados tras reanudacion.")
    else:
        if _platform.input.requires_process_restart_after_resume():
            logger.warning("Los hooks globales requieren reinicio completo del proceso tras reanudacion en este SO.")
        else:
            logger.error("No fue posible restaurar los hooks globales de entrada tras reanudacion.")

    return recovered


def on_mouse_event(event):
    from module_capture.application.flow_capture_service import flow_capture_service
    flow_capture_service.handle_mouse_event(event)


def check_suspend_jump(last_tick_time, icon=None):
    current_time = time.time()
    jump = current_time - last_tick_time
    if jump <= RUNTIME_CONFIG["suspend_jump_threshold_seconds"]:
        return current_time, None

    logger.warning(f"Salto de tiempo detectado ({jump:.1f}s). Probable suspension del OS.")

    if not restore_global_input_after_resume(icon):
        if _platform.input.requires_process_restart_after_resume():
            logger.warning("Reiniciando el capturador para recuperar los hooks globales tras reanudacion...")
        else:
            logger.warning("No fue posible reactivar los hooks globales de entrada tras reanudacion. Reiniciando el capturador...")
        return current_time, "restart"

    return current_time, None


def _try_notify(icon, message):
    if icon and hasattr(icon, "notify"):
        try:
            icon.notify(message, APP_NAME)
        except Exception:
            pass
