import os
import datetime
import time
import mouse
import keyboard
import threading
import tkinter
from core import config, utils
from module_capture.capture_screen import capture_screen
from module_capture import constants as c

class FlowManager:
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
        if state is not None:
            self.is_active = state
        else:
            self.is_active = not self.is_active
            
        if self.is_active:
            self._start_session()
        else:
            self.session_path = None
            self._stop_timers()
        
        return self.is_active

    def _start_session(self):
        base_path = os.path.expandvars(config.get("save_path"))
        now = datetime.datetime.now()
        folder_name = f"{now.strftime('%Y-%m-%d %H%M%S')} Flujo"
        
        subfolders = []
        if config.get("subfolder_month"): subfolders.append(now.strftime("%Y-%m"))
        if config.get("subfolder_day"): subfolders.append(now.strftime("%Y-%m-%d"))
        if config.get("subfolder_hour"): subfolders.append(now.strftime("%Y-%m-%d %H"))
            
        parent_path = os.path.join(base_path, *subfolders) if subfolders else base_path
        self.session_path = os.path.join(parent_path, folder_name)
        
        if not os.path.exists(self.session_path):
            os.makedirs(self.session_path, exist_ok=True)
        print(f"Sesión de flujo iniciada en: {self.session_path}")

    def _stop_timers(self):
        if self._scroll_check_timer: self._scroll_check_timer.cancel()
        if self._velocity_timer: self._velocity_timer.cancel()
        self._is_manual_scrolling = False

    def handle_mouse_event(self, event):
        if not self.is_active: return
        # print(f"Evento mouse detectado: {type(event)}") # Demasiado ruido
        if self._is_paused_by_key(): return

        if isinstance(event, mouse.ButtonEvent):
            self._handle_click(event)
        elif isinstance(event, mouse.WheelEvent):
            self._handle_wheel(event)
        elif isinstance(event, mouse.MoveEvent):
            self._handle_move(event)

    def _is_paused_by_key(self):
        pause_key = config.get("flow_pause_key")
        if not pause_key: return False
        try:
            keys = [k.strip() for k in pause_key.split('+')]
            return all(keyboard.is_pressed(k) for k in keys if k)
        except:
            return False

    def _handle_click(self, event):
        if event.button == 'left':
            mx, my = mouse.get_position()
            
            # Dimensiones del monitor activo para zona de scroll dinámica
            mon_x, mon_y, mon_w, mon_h = utils.get_monitor_at_cursor()
            rel_x = mx - mon_x
            
            if event.event_type == 'down':
                enable_scroll = config.get("enable_scroll_capture")
                is_in_zone = enable_scroll and (rel_x > (mon_w * c.SCROLL_ZONE_WIDTH_RATIO) or \
                             rel_x > (mon_w - c.SCROLL_ZONE_WIDTH_PIXELS))
                
                if is_in_zone:
                    print(f"Inicio de Scroll Manual en X relativa={rel_x}")
                    self._is_manual_scrolling = True
                    self._last_y = my
                    self._last_captured_y = my
                    self._slow_scroll_start_time = time.time()
                    self._capture("Inicio Scroll")
                    self._start_velocity_monitor()
                else:
                    self._capture("Clic")
            
            elif event.event_type == 'up' and self._is_manual_scrolling:
                print("Fin de Scroll Manual.")
                self._is_manual_scrolling = False
                if self._velocity_timer: self._velocity_timer.cancel()
                
                # Solo capturar si nos hemos movido significativamente desde la última captura
                if abs(my - self._last_captured_y) > c.JITTER_THRESHOLD:
                    self._capture("Fin Scroll")
                else:
                    print("Fin de Scroll omitido por redundancia (sin movimiento significativo).")

    def _handle_wheel(self, event):
        if not config.get("enable_scroll_capture"): return
        
        current_time = time.time()
        if current_time - self._last_scroll_time > c.WHEEL_IDLE_TIME:
            self._capture("Inicio Rueda")
        
        self._last_scroll_time = current_time
        
        if self._scroll_check_timer: self._scroll_check_timer.cancel()
        self._scroll_check_timer = threading.Timer(c.WHEEL_SMART_PAUSE_DEBOUNCE, self._on_wheel_pause)
        self._scroll_check_timer.start()

    def _on_wheel_pause(self):
        if self.is_active and not self._is_paused_by_key():
            self._capture("Pausa Rueda")

    def _handle_move(self, event):
        if self._is_manual_scrolling:
            try:
                self._last_y = event.y
            except:
                _, self._last_y = mouse.get_position()

    def _start_velocity_monitor(self):
        if not self._is_manual_scrolling: return
        
        def monitor():
            if not self._is_manual_scrolling: return
            
            y_before = self._last_y
            time.sleep(c.VELOCITY_CHECK_INTERVAL)
            y_after = self._last_y
            
            if self._is_manual_scrolling:
                v = abs(y_after - y_before)
                dist_from_last = abs(y_after - self._last_captured_y)
                
                # Clasificación de estado
                if v < c.JITTER_THRESHOLD:
                    # REPOSO / PAUSA
                    if dist_from_last > c.MIN_DISTANCE_BETWEEN_CAPTURES:
                        print(f"Pausa detectada en scroll manual (Y={y_after})")
                        self._last_captured_y = y_after
                        self._capture("Pausa Scroll")
                    self._slow_scroll_start_time = time.time() # Reset cadencia en reposo
                    
                elif v <= c.SLOW_SCROLL_MAX_SPEED:
                    # SCROLL LENTO (LECTURA)
                    elapsed = time.time() - self._slow_scroll_start_time
                    if elapsed >= c.SLOW_SCROLL_CADENCE_TIME and dist_from_last > c.MIN_DISTANCE_SLOW_SCROLL:
                        print(f"Cadencia de scroll lento alcanzada (2.5s, Y={y_after})")
                        self._last_captured_y = y_after
                        self._capture("Cadencia Scroll Lento")
                        self._slow_scroll_start_time = time.time() # Reset cadencia tras captura
                else:
                    # SCROLL RÁPIDO (BÚSQUEDA)
                    self._slow_scroll_start_time = time.time() # Reset cadencia en rápido

            # Re-agendar
            if self._is_manual_scrolling:
                self._velocity_timer = threading.Timer(0.1, monitor)
                self._velocity_timer.start()

        self._velocity_timer = threading.Timer(c.VELOCITY_CHECK_INTERVAL, monitor)
        self._velocity_timer.daemon = True
        self._velocity_timer.start()

    def _capture(self, reason):
        capture_screen(play_sound=False, flow_session_path=self.session_path)
        print(f"--- Captura automática ({reason}) ---")

flow_manager = FlowManager()
