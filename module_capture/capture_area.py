import tkinter as tk
from PIL import Image, ImageTk, ImageGrab
import datetime
from core import config, utils
import threading
import mouse

class CaptureAreaUI:
    def __init__(self, on_capture_callback=None):
        self.root = tk.Tk()
        self.root.attributes('-fullscreen', True)
        self.root.attributes('-topmost', True)
        self.root.config(cursor="crosshair")
        
        self.on_capture_callback = on_capture_callback
        self.full_screenshot = ImageGrab.grab(all_screens=True)
        
        # Capturar posición y apariencia del mouse en el momento del "congelado" de pantalla
        try:
            self.mouse_pos = mouse.get_position()
            # Obtener escala y datos del cursor real (imagen + hotspot)
            scale = utils.get_dpi_scaling()
            self.cursor_data = utils.get_current_cursor(scale)
        except:
            self.mouse_pos = None
            self.cursor_data = None
            
        self.bg_image = ImageTk.PhotoImage(self.full_screenshot)
        
        self.canvas = tk.Canvas(self.root, highlightthickness=0, bg="black")
        self.canvas.pack(fill="both", expand=True)
        self.canvas.create_image(0, 0, image=self.bg_image, anchor="nw")
        
        # Overlay oscuro mediante máscara (4 rectángulos)
        # Esto permite dejar el centro (selección) totalmente claro
        self.overlay_top = self.canvas.create_rectangle(0, 0, 0, 0, fill="black", stipple="gray50", outline="")
        self.overlay_bottom = self.canvas.create_rectangle(0, 0, 0, 0, fill="black", stipple="gray50", outline="")
        self.overlay_left = self.canvas.create_rectangle(0, 0, 0, 0, fill="black", stipple="gray50", outline="")
        self.overlay_right = self.canvas.create_rectangle(0, 0, 0, 0, fill="black", stipple="gray50", outline="")
        
        # Inicializar máscara cubriendo toda la pantalla
        self._update_mask(self.root.winfo_screenwidth(), self.root.winfo_screenheight(), 0, 0, 0, 0)
        
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
        self.root.bind("<Escape>", lambda e: self.root.destroy())
        
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
        w = self.root.winfo_screenwidth()
        h = self.root.winfo_screenheight()
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
        self._update_mask(self.root.winfo_screenwidth(), self.root.winfo_screenheight(), 
                         self.start_x, self.start_y, event.x, event.y)
        self.update_guides(event)

    def on_release(self, event):
        if self.start_x is None or self.start_y is None:
            self.root.destroy()
            return
            
        x1, x2 = sorted([self.start_x, event.x])
        y1, y2 = sorted([self.start_y, event.y])
        
        self.root.withdraw()
        
        if (x2 - x1) > 5 and (y2 - y1) > 5:
            # Obtener factor de escala
            scale = utils.get_dpi_scaling()
            
            # Recortar usando coordenadas físicas reales
            cropped_img = self.full_screenshot.crop((
                int(x1 * scale), int(y1 * scale), 
                int(x2 * scale), int(y2 * scale)
            ))
            self.save_capture(cropped_img, x_offset=int(x1 * scale), y_offset=int(y1 * scale), scale=scale)
        
        self.root.destroy()

    def save_capture(self, pil_img, x_offset=0, y_offset=0, scale=1.0):
        import os
        try:
            now = datetime.datetime.now()
            # Aplicar overlay de mouse si está configurado y el mouse estaba dentro del área
            if config.get("show_mouse") and self.mouse_pos:
                mx, my = self.mouse_pos
                # Convertir mouse (lógico) a físico
                # Usar el factor de escala obtenido de utils.get_dpi_scaling() para el mouse
                mouse_scale = utils.get_dpi_scaling()
                pmx, pmy = mx * mouse_scale, my * mouse_scale
                
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

def trigger_area_capture(callback=None):
    app = CaptureAreaUI(on_capture_callback=callback)
    app.run()

if __name__ == "__main__":
    trigger_area_capture()
