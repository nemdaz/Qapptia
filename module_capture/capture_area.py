import tkinter as tk
from PIL import Image, ImageTk, ImageGrab
import datetime
from core import config, utils
import threading
import mouse

class CaptureAreaUI:
    def __init__(self, on_capture_callback=None):
        self.root = tk.Tk()
        self.on_capture_callback = on_capture_callback
        
        # Monitor bajo el cursor
        self.monitor_x, self.monitor_y, self.monitor_w, self.monitor_h = utils.get_monitor_at_cursor()
        
        # 2. Configurar ventana para ese monitor específico
        self.root.overrideredirect(True) # Sin bordes
        self.root.geometry(f"{self.monitor_w}x{self.monitor_h}+{self.monitor_x}+{self.monitor_y}")
        self.root.attributes('-topmost', True)
        self.root.config(cursor="crosshair")
        
        # Captura del escritorio virtual
        self.full_screenshot = ImageGrab.grab(all_screens=True)
        
        # Obtener offsets del escritorio virtual para alinear imagen
        self.virtual_x, self.virtual_y = utils.get_virtual_screen_origin()
        
        try:
            self.mouse_pos = mouse.get_position()
            scale = utils.get_dpi_scaling()
            self.cursor_data = utils.get_current_cursor(scale)
        except:
            self.mouse_pos = None
            self.cursor_data = None
            
        self.bg_image = ImageTk.PhotoImage(self.full_screenshot)
        
        self.canvas = tk.Canvas(self.root, highlightthickness=0, bg="black", 
                               width=self.monitor_w, height=self.monitor_h)
        self.canvas.pack(fill="both", expand=True)
        
        # Dibujar captura con offset según el monitor
        img_offset_x = self.virtual_x - self.monitor_x
        img_offset_y = self.virtual_y - self.monitor_y
        self.bg_image_id = self.canvas.create_image(img_offset_x, img_offset_y, image=self.bg_image, anchor="nw")
        
        # Overlay oscuro mediante máscara (4 rectángulos)
        self.overlay_top = self.canvas.create_rectangle(0, 0, 0, 0, fill="black", stipple="gray50", outline="")
        self.overlay_bottom = self.canvas.create_rectangle(0, 0, 0, 0, fill="black", stipple="gray50", outline="")
        self.overlay_left = self.canvas.create_rectangle(0, 0, 0, 0, fill="black", stipple="gray50", outline="")
        self.overlay_right = self.canvas.create_rectangle(0, 0, 0, 0, fill="black", stipple="gray50", outline="")
        
        # Inicializar máscara cubriendo todo el monitor actual
        self._update_mask(self.monitor_w, self.monitor_h, 0, 0, 0, 0)
        
        # Iniciar bucle de seguimiento de monitor (solo antes de iniciar el drag)
        self.root.after(100, self.check_monitor_change)
        
        self.start_x = None
        self.start_y = None
        self.rect_border = None
        
        # Guías (Crosshairs)
        self.guide_h = self.canvas.create_line(0, 0, 0, 0, fill="cyan", dash=(2, 2))
        self.guide_v = self.canvas.create_line(0, 0, 0, 0, fill="cyan", dash=(2, 2))
        
        self.canvas.bind("<ButtonPress-1>", self.on_press)
        self.canvas.bind("<B1-Motion>", self.on_drag)
        self.canvas.bind("<ButtonRelease-1>", self.on_release)
        self.canvas.bind("<Motion>", self.update_guides)
        
        # Secuestrar eventos y forzar foco post-renderizado 
        self.root.grab_set()
        self.root.after(50, self.root.focus_force)
        self.canvas.focus_set()
        
        # Intercepción de tecla ESC a nivel global
        import keyboard
        self.esc_hook = keyboard.on_press_key("esc", lambda e: self.root.after(0, self.close), suppress=True)
        
        self.root.bind("<Escape>", lambda e: self.close())
        self.canvas.bind("<Escape>", lambda e: self.close())

    def check_monitor_change(self):
        """Salto dinámico entre monitores."""
        if self.start_x is not None or not self.root.winfo_exists():
            return
            
        new_mx, new_my, new_mw, new_mh = utils.get_monitor_at_cursor()
        if (new_mx, new_my) != (self.monitor_x, self.monitor_y):
            self.monitor_x, self.monitor_y, self.monitor_w, self.monitor_h = new_mx, new_my, new_mw, new_mh
            
            # Actualizar geometría y contenido del canvas
            self.canvas.config(width=self.monitor_w, height=self.monitor_h)
            img_offset_x = self.virtual_x - self.monitor_x
            img_offset_y = self.virtual_y - self.monitor_y
            self.canvas.coords(self.bg_image_id, img_offset_x, img_offset_y)
            self._update_mask(self.monitor_w, self.monitor_h, 0, 0, 0, 0)
            self.root.geometry(f"{self.monitor_w}x{self.monitor_h}+{self.monitor_x}+{self.monitor_y}")
            self.root.update()
            
        self.root.after(100, self.check_monitor_change)

    def close(self):
        """Limpieza y destrucción garantizada."""
        import keyboard
        try:
            keyboard.unhook(self.esc_hook)
        except Exception:
            pass
        self.root.destroy()
        
    def _update_mask(self, sw, sh, x1, y1, x2, y2):
        """Actualiza los 4 rectángulos para dejar un hueco claro en (x1, y1) -> (x2, y2)"""
        # Asegurar orden de coordenadas
        xa, xb = sorted([x1, x2])
        ya, yb = sorted([y1, y2])
        
        self.canvas.coords(self.overlay_top, 0, 0, sw, ya)
        self.canvas.coords(self.overlay_bottom, 0, yb, sw, sh)
        self.canvas.coords(self.overlay_left, 0, ya, xa, yb)
        self.canvas.coords(self.overlay_right, xb, ya, sw, yb)

    def update_guides(self, event):
        w = self.monitor_w
        h = self.monitor_h
        self.canvas.coords(self.guide_h, 0, event.y, w, event.y)
        self.canvas.coords(self.guide_v, event.x, 0, event.x, h)
        self.canvas.tag_raise(self.guide_h)
        self.canvas.tag_raise(self.guide_v)

    def on_press(self, event):
        self.start_x = event.x
        self.start_y = event.y
        if self.rect_border: self.canvas.delete(self.rect_border)
        self.rect_border = self.canvas.create_rectangle(self.start_x, self.start_y, self.start_x, self.start_y, 
                                                     outline="cyan", width=1)

    def on_drag(self, event):
        self.canvas.coords(self.rect_border, self.start_x, self.start_y, event.x, event.y)
        self._update_mask(self.monitor_w, self.monitor_h, 
                         self.start_x, self.start_y, event.x, event.y)
        self.update_guides(event)

    def on_release(self, event):
        if self.start_x is None or self.start_y is None:
            self.close()
            return
            
        x1, x2 = sorted([self.start_x, event.x])
        y1, y2 = sorted([self.start_y, event.y])
        
        self.root.withdraw()
        
        if (x2 - x1) > 5 and (y2 - y1) > 5:
            scale = utils.get_dpi_scaling()
            
            # Coordenadas globales y recorte
            global_x1, global_y1 = self.monitor_x + x1, self.monitor_y + y1
            global_x2, global_y2 = self.monitor_x + x2, self.monitor_y + y2
            img_x1, img_y1 = global_x1 - self.virtual_x, global_y1 - self.virtual_y
            img_x2, img_y2 = global_x2 - self.virtual_x, global_y2 - self.virtual_y

            cropped_img = self.full_screenshot.crop((
                int(img_x1 * scale), int(img_y1 * scale), 
                int(img_x2 * scale), int(img_y2 * scale)
            ))
            self.save_capture(cropped_img, x_offset=int(img_x1 * scale), y_offset=int(img_y1 * scale), scale=scale)
        
        self.close()

    def save_capture(self, pil_img, x_offset=0, y_offset=0, scale=1.0):
        import os
        try:
            now = datetime.datetime.now()
            if config.get("show_mouse") and self.mouse_pos:
                mx, my = self.mouse_pos
                scale = utils.get_dpi_scaling()
                pmx, pmy = mx * scale, my * scale
                
                # Comprobamos si el mouse está dentro del recorte (dimensiones físicas)
                if x_offset <= pmx <= x_offset + pil_img.width and y_offset <= pmy <= y_offset + pil_img.height:
                    hl = config.get("highlight_mouse")
                    pil_img = utils.draw_mouse_overlay(pil_img, pmx - x_offset, pmy - y_offset, hl, cursor_data=self.cursor_data)
            
            save_dir = utils.get_save_directory(config.get("save_path"), now)
            filename = utils.parse_filename_format(config.get("filename_format"), now).replace(".png", "_area.png")
            filepath = os.path.join(save_dir, filename)
            
            pil_img.save(filepath, 'PNG', quality=config.get("image_quality"))
            
            if self.on_capture_callback:
                self.on_capture_callback(filepath)
            
            threading.Thread(target=utils.play_beep_async, daemon=True).start()
            print(f"Área capturada: {filepath}")
        except Exception as e:
            print(f"Error al guardar captura de área: {e}")

    def run(self):
        self.root.mainloop()

