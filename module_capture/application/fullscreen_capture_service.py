import datetime
import os
import time

import mouse
from PIL import ImageGrab

from core import config, utils
from core.logger import logger
from module_capture import constants


class FullscreenCaptureService:
    def capture_fullscreen(self, play_sound=True, output_directory=None):
        try:
            now = datetime.datetime.now()
            target_directory = output_directory or utils.get_save_directory(config.get("save_path"), now)
            filename = utils.parse_filename_format(config.get("filename_format"), now)
            output_path = os.path.join(target_directory, filename)

            screen_image = self._capture_active_monitor()
            screen_image.save(output_path, "PNG", quality=config.get("image_quality"))

            if play_sound:
                utils.play_beep_async()

            logger.success(constants.CAPTURE_MESSAGES["screen_capture_success"].format(path=output_path))
            return output_path
        except Exception as exc:
            logger.error(constants.CAPTURE_MESSAGES["screen_capture_error"].format(error=exc))
            return None

    def capture_with_timer(self):
        config.load_config()
        timer = config.get("manual_timer")
        if timer > 0:
            logger.info(constants.CAPTURE_MESSAGES["screen_capture_wait"].format(timer=timer))
            time.sleep(timer)
        else:
            logger.info(constants.CAPTURE_MESSAGES["screen_capture_now"])
        return self.capture_fullscreen()

    def _capture_active_monitor(self):
        monitor_x, monitor_y, monitor_width, monitor_height = utils.get_monitor_at_cursor()
        virtual_x, virtual_y = utils.get_virtual_screen_origin()

        full_image = ImageGrab.grab(all_screens=True)
        crop_x = monitor_x - virtual_x
        crop_y = monitor_y - virtual_y
        image = full_image.crop((crop_x, crop_y, crop_x + monitor_width, crop_y + monitor_height))

        if not config.get("show_mouse"):
            return image

        try:
            mouse_x, mouse_y = mouse.get_position()
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

