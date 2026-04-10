from core.logger import logger


_REGISTERED_HOTKEYS = []


def remember_hotkey_registration(hotkey, hotkey_handle, description=""):
    _REGISTERED_HOTKEYS.append((hotkey, hotkey_handle, description))


def clear_registered_hotkeys(input_service):
    while _REGISTERED_HOTKEYS:
        hotkey, hotkey_handle, description = _REGISTERED_HOTKEYS.pop()
        try:
            input_service.remove_hotkey(hotkey_handle)
            desc_str = f" ({description})" if description else ""
            logger.debug(f"Hotkey limpiado: '{hotkey}'{desc_str}")
        except Exception as exc:
            logger.debug(f"No se pudo limpiar hotkey '{hotkey}': {exc}")


def reset_global_input_hooks(input_service):
    clear_registered_hotkeys(input_service)

    try:
        input_service.unhook_all_hotkeys()
    except Exception as exc:
        logger.debug(f"No se pudieron limpiar hotkeys previos: {exc}")

    try:
        input_service.unhook_all_mouse()
    except Exception as exc:
        logger.debug(f"No se pudieron limpiar hooks de mouse previos: {exc}")


def restore_global_input_hooks_in_process(
    input_service,
    register_hotkeys_callback,
    mouse_callback,
    max_attempts=2,
    retry_delay_seconds=0.25,
):
    for attempt in range(1, max_attempts + 1):
        reset_global_input_hooks(input_service)

        hotkeys_ok = bool(register_hotkeys_callback())
        mouse_ok = True
        try:
            input_service.hook_mouse(mouse_callback)
        except Exception as exc:
            mouse_ok = False
            logger.error(f"No se pudo reenganchar hook global de mouse: {exc}")

        if hotkeys_ok and mouse_ok:
            if attempt > 1:
                logger.info(f"Hooks globales restaurados en intento {attempt}/{max_attempts}.")
            return True

        if attempt < max_attempts:
            import time
            time.sleep(max(0.0, retry_delay_seconds))

    return False
