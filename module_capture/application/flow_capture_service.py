import datetime
import os
import threading
import time

import keyboard
import mouse

from core import config, utils
from core.logger import logger
from module_capture import constants
from module_capture.application.fullscreen_capture_service import fullscreen_capture_service
from module_capture.domain import flow_constants


class FlowCaptureService:
    def __init__(self):
        self.is_active = False
        self.session_path = None
        self._last_scroll_time = 0
        self._scroll_check_timer = None
        self._is_manual_scrolling = False
        self._last_y = 0
        self._velocity_timer = None
        self._last_captured_y = -1
        self._slow_scroll_start_time = 0

    def toggle(self, state=None):
        self.is_active = (not self.is_active) if state is None else state
        if self.is_active:
            self._start_session()
        else:
            self.session_path = None
            self._stop_timers()
        return self.is_active

    def handle_mouse_event(self, event):
        if not self.is_active or self._is_paused_by_shortcut():
            return

        if isinstance(event, mouse.ButtonEvent):
            self._handle_click(event)
        elif isinstance(event, mouse.WheelEvent):
            self._handle_wheel()
        elif isinstance(event, mouse.MoveEvent):
            self._handle_move(event)

    def _start_session(self):
        base_path = os.path.expandvars(config.get("save_path"))
        now = datetime.datetime.now()
        folder_name = f"{now.strftime('%Y-%m-%d %H%M%S')}{constants.FLOW_MESSAGES['session_suffix']}"

        subfolders = []
        if config.get("subfolder_month"):
            subfolders.append(now.strftime("%Y-%m"))
        if config.get("subfolder_day"):
            subfolders.append(now.strftime("%Y-%m-%d"))
        if config.get("subfolder_hour"):
            subfolders.append(now.strftime("%Y-%m-%d %H"))

        parent_path = os.path.join(base_path, *subfolders) if subfolders else base_path
        self.session_path = os.path.join(parent_path, folder_name)
        os.makedirs(self.session_path, exist_ok=True)
        logger.info(constants.FLOW_MESSAGES["session_started"].format(path=self.session_path))

    def _stop_timers(self):
        if self._scroll_check_timer:
            self._scroll_check_timer.cancel()
        if self._velocity_timer:
            self._velocity_timer.cancel()
        self._is_manual_scrolling = False

    def _is_paused_by_shortcut(self):
        pause_shortcut = config.get("shortcut_flow_pause")
        if not pause_shortcut:
            return False
        try:
            keys = [key.strip() for key in pause_shortcut.split("+") if key.strip()]
            return all(keyboard.is_pressed(key) for key in keys)
        except Exception:
            return False

    def _handle_click(self, event):
        if event.button != "left":
            return

        mouse_x, mouse_y = mouse.get_position()
        monitor_x, _, monitor_width, _ = utils.get_monitor_at_cursor()
        relative_x = mouse_x - monitor_x

        if event.event_type == "down":
            enable_scroll = config.get("enable_scroll_capture")
            in_scroll_zone = enable_scroll and (
                relative_x > (monitor_width * flow_constants.SCROLL_ZONE_WIDTH_RATIO)
                or relative_x > (monitor_width - flow_constants.SCROLL_ZONE_WIDTH_PIXELS)
            )

            if in_scroll_zone:
                logger.debug(constants.FLOW_MESSAGES["manual_scroll_start"].format(x=relative_x))
                self._is_manual_scrolling = True
                self._last_y = mouse_y
                self._last_captured_y = mouse_y
                self._slow_scroll_start_time = time.time()
                self._capture(constants.FLOW_MESSAGES["reasons"]["manual_scroll_start"])
                self._start_velocity_monitor()
            else:
                self._capture(constants.FLOW_MESSAGES["reasons"]["click"])

        elif event.event_type == "up" and self._is_manual_scrolling:
            logger.debug(constants.FLOW_MESSAGES["manual_scroll_end"])
            self._is_manual_scrolling = False
            if self._velocity_timer:
                self._velocity_timer.cancel()

            if abs(mouse_y - self._last_captured_y) > flow_constants.JITTER_THRESHOLD:
                self._capture(constants.FLOW_MESSAGES["reasons"]["manual_scroll_end"])
            else:
                logger.debug(constants.FLOW_MESSAGES["manual_scroll_omitted"])

    def _handle_wheel(self):
        if not config.get("enable_scroll_capture"):
            return

        current_time = time.time()
        if current_time - self._last_scroll_time > flow_constants.WHEEL_IDLE_TIME:
            self._capture(constants.FLOW_MESSAGES["reasons"]["wheel_start"])
        self._last_scroll_time = current_time

        if self._scroll_check_timer:
            self._scroll_check_timer.cancel()
        self._scroll_check_timer = threading.Timer(flow_constants.WHEEL_SMART_PAUSE_DEBOUNCE, self._on_wheel_pause)
        self._scroll_check_timer.start()

    def _on_wheel_pause(self):
        if self.is_active and not self._is_paused_by_shortcut():
            self._capture(constants.FLOW_MESSAGES["reasons"]["wheel_pause"])

    def _handle_move(self, event):
        if self._is_manual_scrolling:
            try:
                self._last_y = event.y
            except Exception:
                _, self._last_y = mouse.get_position()

    def _start_velocity_monitor(self):
        if not self._is_manual_scrolling or not self.is_active:
            return
        if self._velocity_timer and self._velocity_timer.is_alive():
            return

        def monitor():
            if not self._is_manual_scrolling or not self.is_active:
                return

            y_before = self._last_y
            time.sleep(flow_constants.VELOCITY_CHECK_INTERVAL)
            y_after = self._last_y

            if self._is_manual_scrolling and self.is_active:
                velocity = abs(y_after - y_before)
                distance_from_last = abs(y_after - self._last_captured_y)

                if velocity < flow_constants.JITTER_THRESHOLD:
                    if distance_from_last > flow_constants.MIN_DISTANCE_BETWEEN_CAPTURES:
                        self._last_captured_y = y_after
                        self._capture(constants.FLOW_MESSAGES["reasons"]["manual_scroll_pause"])
                    self._slow_scroll_start_time = time.time()
                elif velocity <= flow_constants.SLOW_SCROLL_MAX_SPEED:
                    elapsed = time.time() - self._slow_scroll_start_time
                    if elapsed >= flow_constants.SLOW_SCROLL_CADENCE_TIME and distance_from_last > flow_constants.MIN_DISTANCE_SLOW_SCROLL:
                        self._last_captured_y = y_after
                        self._capture(constants.FLOW_MESSAGES["reasons"]["slow_scroll_cadence"])
                        self._slow_scroll_start_time = time.time()
                else:
                    self._slow_scroll_start_time = time.time()

            if self._is_manual_scrolling and self.is_active:
                self._velocity_timer = threading.Timer(0.1, monitor)
                self._velocity_timer.daemon = True
                self._velocity_timer.start()

        self._velocity_timer = threading.Timer(0.1, monitor)
        self._velocity_timer.daemon = True
        self._velocity_timer.start()

    def _capture(self, reason):
        fullscreen_capture_service.capture_fullscreen(play_sound=False, output_directory=self.session_path)
        logger.debug(constants.FLOW_MESSAGES["auto_capture"].format(reason=reason))


flow_capture_service = FlowCaptureService()
