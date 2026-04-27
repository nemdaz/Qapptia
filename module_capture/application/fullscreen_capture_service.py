import datetime
import os

from core import config, utils
from core.constants import APP_NAME
from core.logger import logger
from core.platform import get_platform_services
from module_capture import constants

_platform = get_platform_services()


class FullscreenCaptureService:
    def capture_fullscreen(self, play_sound=True, output_directory=None):
        try:
            if play_sound:
                utils.play_beep_async()

            now = datetime.datetime.now()
            target_directory = output_directory or utils.get_save_directory(config.get("save_path"), now)
            filename = utils.parse_filename_format(config.get("filename_format"), now)
            output_path = os.path.join(target_directory, filename)

            screen_image = self._capture_active_monitor()
            screen_image.save(output_path, "PNG")

            logger.success(constants.CAPTURE_MESSAGES["screen_capture_success"].format(path=output_path))
            return output_path
        except Exception as exc:
            logger.error(constants.CAPTURE_MESSAGES["screen_capture_error"].format(error=exc))
            _platform.desktop.show_info_message(APP_NAME, constants.CAPTURE_MESSAGES["capture_user_error"])
            return None

    def _capture_active_monitor(self):
        monitor_x, monitor_y, monitor_width, monitor_height = utils.get_monitor_at_cursor()
        virtual_x, virtual_y = utils.get_virtual_screen_origin()

        full_image = _platform.screen.capture_all_screens()
        crop_x = monitor_x - virtual_x
        crop_y = monitor_y - virtual_y
        image = full_image.crop((crop_x, crop_y, crop_x + monitor_width, crop_y + monitor_height))

        if not config.get("show_mouse"):
            return image

        try:
            mouse_x, mouse_y = _platform.input.get_mouse_position()
            scale = utils.get_dpi_scaling()
            cursor_data = utils.get_current_cursor(scale)
            relative_x = (mouse_x - monitor_x) * scale
            relative_y = (mouse_y - monitor_y) * scale
            return utils.draw_mouse_overlay(
                image,
                relative_x,
                relative_y,
                config.get("highlight_mouse"),
                cursor_data=cursor_data,
                highlight_style=constants.CURSOR_HIGHLIGHT_STYLE,
            )
        except Exception as exc:
            logger.error(constants.CAPTURE_MESSAGES["screen_mouse_error"].format(error=exc))
            return image


fullscreen_capture_service = FullscreenCaptureService()
