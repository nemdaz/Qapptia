import datetime
import os
import threading

from PIL import ImageQt
from PySide6.QtCore import QEventLoop, QRect, Qt, QTimer, Signal
from PySide6.QtGui import QColor, QKeySequence, QPainter, QPen, QPixmap
from PySide6.QtWidgets import QApplication, QWidget

from core import config, utils
from core.constants import APP_NAME
from core.logger import logger
from core.platform import get_platform_services
from module_capture import constants

_platform = get_platform_services()


class AreaSelectorWindow(QWidget):
    _cancel_requested = Signal()

    def __init__(self, on_capture_callback=None):
        super().__init__(None)
        self._on_capture_callback = on_capture_callback
        self._selection_origin = None
        self._selection_end = None
        self._current_x = 0
        self._current_y = 0
        self._is_cancelling = False

        self._load_screen_context()
        self._configure_window()

        self._monitor_timer = QTimer(self)
        self._monitor_timer.timeout.connect(self._check_monitor_change)
        self._monitor_timer.start(constants.AREA_SELECTOR_STYLE["monitor_poll_interval_ms"])
        self._cancel_requested.connect(self._cancel_capture)

        self._esc_hook = _platform.input.on_press_key("esc", self._on_esc_key, suppress=True)

    def _on_esc_key(self, _event):
        self._cancel_requested.emit()

    def _cancel_capture(self):
        if self._is_cancelling:
            return
        self._is_cancelling = True
        self.close()

    def _load_screen_context(self):
        self._monitor_x, self._monitor_y, self._monitor_width, self._monitor_height = utils.get_monitor_at_cursor()
        self._virtual_x, self._virtual_y = utils.get_virtual_screen_origin()
        self._full_screenshot = _platform.screen.capture_all_screens()
        self._background_pixmap = QPixmap.fromImage(ImageQt.ImageQt(self._full_screenshot))

        try:
            self._mouse_pos = _platform.input.get_mouse_position()
            scale = utils.get_dpi_scaling()
            self._cursor_data = utils.get_current_cursor(scale)
        except Exception:
            self._mouse_pos = None
            self._cursor_data = None

    def _configure_window(self):
        self.setWindowFlags(Qt.FramelessWindowHint | Qt.WindowStaysOnTopHint | Qt.Tool)
        self.setAttribute(Qt.WA_TranslucentBackground, True)
        self.setAttribute(Qt.WA_DeleteOnClose, True)
        self.setCursor(Qt.CrossCursor)
        self.setGeometry(self._monitor_x, self._monitor_y, self._monitor_width, self._monitor_height)
        self.setMouseTracking(True)
        self.grabMouse()
        self.grabKeyboard()

    def paintEvent(self, event):
        painter = QPainter(self)
        source_rect = QRect(
            self._monitor_x - self._virtual_x,
            self._monitor_y - self._virtual_y,
            self._monitor_width,
            self._monitor_height,
        )
        painter.drawPixmap(self.rect(), self._background_pixmap, source_rect)

        selection = self._selection_rect()
        painter.fillRect(self.rect(), QColor(*constants.AREA_SELECTOR_STYLE["overlay_rgba"]))

        if not selection.isNull():
            painter.drawPixmap(
                selection,
                self._background_pixmap,
                QRect(
                    source_rect.x() + selection.x(),
                    source_rect.y() + selection.y(),
                    selection.width(),
                    selection.height(),
                ),
            )
            painter.setPen(QPen(QColor(constants.AREA_SELECTOR_STYLE["crosshair_color"]), constants.AREA_SELECTOR_STYLE["stroke_width"]))
            painter.drawRect(selection)

        painter.setPen(QPen(QColor(constants.AREA_SELECTOR_STYLE["crosshair_color"]), constants.AREA_SELECTOR_STYLE["stroke_width"], Qt.DashLine))
        painter.drawLine(0, self._current_y, self.width(), self._current_y)
        painter.drawLine(self._current_x, 0, self._current_x, self.height())

    def mousePressEvent(self, event):
        if event.button() != Qt.LeftButton:
            return
        point = event.position().toPoint()
        self._selection_origin = point
        self._selection_end = point
        self.update()

    def mouseMoveEvent(self, event):
        point = event.position().toPoint()
        self._current_x = point.x()
        self._current_y = point.y()
        if self._selection_origin is not None:
            self._selection_end = point
            self.update()

    def mouseReleaseEvent(self, event):
        if event.button() != Qt.LeftButton or self._selection_origin is None:
            self.close()
            return

        self._selection_end = event.position().toPoint()
        selection = self._selection_rect()
        self.hide()

        if selection.width() > constants.AREA_SELECTOR_STYLE["min_selection_size"] and selection.height() > constants.AREA_SELECTOR_STYLE["min_selection_size"]:
            self._save_selection(selection)

        self.close()

    def keyPressEvent(self, event):
        if event.key() == Qt.Key_Escape or event.matches(QKeySequence.Cancel):
            self._cancel_capture()
            return
        super().keyPressEvent(event)

    def closeEvent(self, event):
        try:
            _platform.input.unhook_key_listener(self._esc_hook)
        except Exception:
            pass
        self._monitor_timer.stop()
        self.releaseMouse()
        self.releaseKeyboard()
        super().closeEvent(event)

    def _selection_rect(self):
        if self._selection_origin is None or self._selection_end is None:
            return QRect()
        left = min(self._selection_origin.x(), self._selection_end.x())
        top = min(self._selection_origin.y(), self._selection_end.y())
        right = max(self._selection_origin.x(), self._selection_end.x())
        bottom = max(self._selection_origin.y(), self._selection_end.y())
        return QRect(left, top, right - left, bottom - top)

    def _check_monitor_change(self):
        if self._selection_origin is not None:
            return

        new_x, new_y, new_width, new_height = utils.get_monitor_at_cursor()
        if (new_x, new_y) == (self._monitor_x, self._monitor_y):
            return

        self._monitor_x = new_x
        self._monitor_y = new_y
        self._monitor_width = new_width
        self._monitor_height = new_height
        self.setGeometry(self._monitor_x, self._monitor_y, self._monitor_width, self._monitor_height)
        self.update()

    def _save_selection(self, selection):
        try:
            utils.play_shutter_async()

            scale = utils.get_dpi_scaling()
            global_x1 = self._monitor_x + selection.x()
            global_y1 = self._monitor_y + selection.y()
            global_x2 = global_x1 + selection.width()
            global_y2 = global_y1 + selection.height()

            image_x1 = global_x1 - self._virtual_x
            image_y1 = global_y1 - self._virtual_y
            image_x2 = global_x2 - self._virtual_x
            image_y2 = global_y2 - self._virtual_y

            full_screenshot = self._full_screenshot.copy()
            mouse_pos = self._mouse_pos
            cursor_data = self._cursor_data
            callback = self._on_capture_callback

            def _worker():
                try:
                    cropped_image = full_screenshot.crop(
                        (
                            int(image_x1 * scale),
                            int(image_y1 * scale),
                            int(image_x2 * scale),
                            int(image_y2 * scale),
                        )
                    )
                    self._save_capture_async(cropped_image, int(image_x1 * scale), int(image_y1 * scale), mouse_pos, cursor_data, callback)
                except Exception as exc:
                    logger.error(constants.CAPTURE_MESSAGES["area_crop_error"].format(error=exc))

            threading.Thread(target=_worker, daemon=True).start()
        except Exception as exc:
            logger.error(constants.CAPTURE_MESSAGES["area_crop_error"].format(error=exc))

    def _save_capture_async(self, image, x_offset, y_offset, mouse_pos, cursor_data, callback):
        try:
            now = datetime.datetime.now()

            if config.get("show_mouse") and mouse_pos:
                mouse_x, mouse_y = mouse_pos
                scale = utils.get_dpi_scaling()
                physical_x = mouse_x * scale
                physical_y = mouse_y * scale

                if x_offset <= physical_x <= x_offset + image.width and y_offset <= physical_y <= y_offset + image.height:
                    image = utils.draw_mouse_overlay(
                        image,
                        physical_x - x_offset,
                        physical_y - y_offset,
                        config.get("highlight_mouse"),
                        cursor_data=cursor_data,
                        highlight_style=constants.CURSOR_HIGHLIGHT_STYLE,
                    )

            save_directory = utils.get_save_directory(config.get("save_path"), now)
            filename = utils.parse_filename_format(config.get("filename_format"), now).replace(".png", "_area.png")
            output_path = os.path.join(save_directory, filename)
            image.save(output_path, "PNG")

            if callback:
                app = QApplication.instance()
                if app:
                    QTimer.singleShot(0, app, lambda: callback(output_path))

            logger.success(constants.CAPTURE_MESSAGES["area_save_success"].format(path=output_path))
        except Exception as exc:
            logger.error(constants.CAPTURE_MESSAGES["area_save_error"].format(error=exc))
            _platform.desktop.show_info_message(APP_NAME, constants.CAPTURE_MESSAGES["capture_user_error"])


def run_area_selector(on_capture_callback=None):
    app = QApplication.instance()
    if app is None:
        app = QApplication([])

    selector = AreaSelectorWindow(on_capture_callback=on_capture_callback)
    event_loop = QEventLoop()
    selector.destroyed.connect(event_loop.quit)
    selector.show()
    selector.activateWindow()
    selector.raise_()
    event_loop.exec()
