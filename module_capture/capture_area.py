import tkinter as tk
from PIL import Image, ImageTk, ImageGrab
import os
import datetime
from core import config, utils
import threading

class CaptureAreaUI:
    def __init__(self, on_capture_callback=None):
        self.root = tk.Tk()
        self.root.attributes('-fullscreen', True)
        self.root.attributes('-topmost', True)
        self.root.config(cursor="crosshair")
        
        self.on_capture_callback = on_capture_callback
        self.full_screenshot = ImageGrab.grab(all_screens=True)
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
        x1, x2 = sorted([self.start_x, event.x])
        y1, y2 = sorted([self.start_y, event.y])
        
        self.root.withdraw()
        
        if (x2 - x1) > 5 and (y2 - y1) > 5:
            cropped_img = self.full_screenshot.crop((x1, y1, x2, y2))
            self.save_capture(cropped_img)
        
        self.root.destroy()

    def save_capture(self, pil_img):
        try:
            now = datetime.datetime.now()
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
