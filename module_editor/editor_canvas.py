import tkinter as tk
import json
import os
import math
from PIL import Image, ImageTk, ImageDraw
from module_editor import utils, constants

class VectorCanvas(tk.Canvas):
    """Motor de dibujo vectorial sobre imágenes con soporte para zoom y persistencia JSON."""
    
    def __init__(self, master, on_zoom_callback=None, **kwargs):
        super().__init__(master, bg=constants.CANVAS_BG_COLOR, highlightthickness=0, **kwargs)
        self.master = master
        self.on_zoom_callback = on_zoom_callback
        
        # Estado de imagen
        self.img_item = None
        self.current_pil_image = None
        self.tk_image = None
        self.image_path = None
        
        # Geometría y Zoom
        self.zoom_level = 1.0 
        self.base_ratio = 1.0 
        self.ratio = 1.0      
        self.img_x, self.img_y = 0, 0
        self.vectors = [] 
        
        # Interacción
        self.draw_mode = None
        self.selected_vector_id = None
        self.active_grip = None
        self._drag_start_x = 0
        self._drag_start_y = 0

        self._setup_bindings()

    def _setup_bindings(self):
        self.bind("<Configure>", self.on_resize)
        self.bind("<ButtonPress-1>", self.on_press)
        self.bind("<B1-Motion>", self.on_drag)
        self.bind("<ButtonRelease-1>", self.on_release)
        self.bind("<Delete>", self.delete_selected_vector)
        # Zoom (Windows y Linux)
        self.bind("<Control-MouseWheel>", self.on_zoom)
        self.bind("<Control-Button-4>", self.on_zoom)
        self.bind("<Control-Button-5>", self.on_zoom)

    def load_image(self, pil_image, path):
        self.current_pil_image = pil_image
        self.image_path = path
        self.vectors = []
        self.zoom_level = 1.0
        self._load_vector_metadata()
        self._render(force_resize=True)

    def _render(self, force_resize=True):
        if not self.current_pil_image: return
        w, h = self.winfo_width(), self.winfo_height()
        if w < 10 or h < 10: return

        if force_resize or not hasattr(self, '_last_size') or self._last_size != (w, h):
            self._update_background_image(w, h)
            self._last_size = (w, h)
        self._redraw_vectors()

    def _update_background_image(self, cw, ch):
        iw, ih = self.current_pil_image.size
        
        # Calcular ratio Fit
        self.base_ratio = min(cw/iw, ch/ih)
        if self.base_ratio > 1: self.base_ratio = 1
        self.ratio = self.base_ratio * self.zoom_level
        
        nw, nh = int(iw * self.ratio), int(ih * self.ratio)
        self.img_x, self.img_y = max((cw - nw) // 2, 0), max((ch - nh) // 2, 0)
        
        resized = self.current_pil_image.resize((nw, nh), Image.Resampling.LANCZOS)
        self.tk_image = ImageTk.PhotoImage(resized)
        
        self.delete("background")
        self.img_item = self.create_image(self.img_x, self.img_y, image=self.tk_image, anchor="nw", tags="background")
        self.tag_lower("background")
        
        # Sincronizar región de scroll
        sr_w, sr_h = max(nw + self.img_x*2, cw), max(nh + self.img_y*2, ch)
        self.configure(scrollregion=(0, 0, sr_w, sr_h))

    def on_zoom(self, event):
        if not self.current_pil_image: return
        cx, cy = self.canvasx(event.x), self.canvasy(event.y)
        x_real, y_real = (cx - self.img_x) / self.ratio, (cy - self.img_y) / self.ratio
        
        old_zoom = self.zoom_level
        factor = constants.ZOOM_STEP if (event.num == 4 or event.delta > 0) else (1/constants.ZOOM_STEP)
        self.zoom_level = max(constants.ZOOM_MIN, min(self.zoom_level * factor, constants.ZOOM_MAX))
        
        if self.zoom_level != old_zoom:
            self._render(force_resize=True)
            # Centrar sobre el punto del mouse
            new_cx, new_cy = self.img_x + (x_real * self.ratio), self.img_y + (y_real * self.ratio)
            sr = [float(x) for x in self.cget("scrollregion").split()]
            if sr[2] > 0: self.xview_moveto((self.canvasx(0) + (new_cx - cx)) / sr[2])
            if sr[3] > 0: self.yview_moveto((self.canvasy(0) + (new_cy - cy)) / sr[3])
            if self.on_zoom_callback: self.on_zoom_callback()

    def on_resize(self, event):
        self._render(force_resize=True)

    def set_draw_mode(self, mode):
        self.draw_mode = mode
        self.configure(cursor="crosshair" if mode else "")

    def _redraw_vectors(self):
        self.delete("vector", "grip")
        v_width = max(1, int(constants.VECTOR_WIDTH * self.zoom_level))
        
        for v in self.vectors:
            x1, y1, x2, y2 = v["coords"]
            px1, py1 = self.img_x + (x1 * self.ratio), self.img_y + (y1 * self.ratio)
            px2, py2 = self.img_x + (x2 * self.ratio), self.img_y + (y2 * self.ratio)
            
            if v["type"] == "rect":
                pts = [px1, py1, px2, py1, px2, py2, px1, py2, px1, py1]
                self.create_line(pts, fill=v["color"], width=v_width, capstyle=tk.ROUND, joinstyle=tk.ROUND, tags=("vector", v["id"]))
            elif v["type"] == "arrow":
                self.create_line(px1, py1, px2, py2, fill=v["color"], width=v_width, capstyle=tk.ROUND, joinstyle=tk.ROUND, tags=("vector", v["id"]))
                # Aletas de la flecha
                ang = math.atan2(py2 - py1, px2 - px1)
                wlen = constants.ARROW_WING_LEN * self.zoom_level
                for a in [-math.pi/6, math.pi/6]:
                    self.create_line(px2, py2, px2 - wlen * math.cos(ang-a), py2 - wlen * math.sin(ang-a), 
                                     fill=v["color"], width=v_width, capstyle=tk.ROUND, joinstyle=tk.ROUND, tags=("vector", v["id"]))
                
            if self.selected_vector_id == v["id"]:
                self._draw_grips(px1, py1, px2, py2, v["id"], v["type"])

    def _draw_grips(self, x1, y1, x2, y2, v_id, v_type):
        r = constants.GRIP_SIZE
        corners = [(x1, y1, "tl"), (x2, y1, "tr"), (x1, y2, "bl"), (x2, y2, "br")] if v_type == "rect" else [(x1, y1, "start"), (x2, y2, "end")]
        for cx, cy, ctype in corners:
            self.create_rectangle(cx-r, cy-r, cx+r, cy+r, fill="white", outline="black", tags=("grip", f"grip_{ctype}_{v_id}"))

    def on_press(self, event):
        self.focus_set()
        self._is_new = False
        cx, cy = self.canvasx(event.x), self.canvasy(event.y)
        
        # 1. Grips
        for g in self.find_withtag("grip"):
            if self._is_point_in_bbox(cx, cy, self.bbox(g)):
                self.active_grip = self.gettags(g)[1].split("_")[1]
                self._drag_start_x, self._drag_start_y = cx, cy
                return
                
        # 2. Nuevo Vector
        if self.draw_mode:
            rx, ry = (cx - self.img_x) / self.ratio, (cy - self.img_y) / self.ratio
            new_id = f"{self.draw_mode}_{len(self.vectors)}"
            self.vectors.append({"type": self.draw_mode, "id": new_id, "coords": [rx, ry, rx, ry], "color": constants.DEFAULT_VECTOR_COLOR})
            self.selected_vector_id, self.active_grip = new_id, ("br" if self.draw_mode == "rect" else "end")
            self._drag_start_x, self._drag_start_y = cx, cy
            self._is_new = True
            self._render(force_resize=False)
            return
            
        # 3. Seleccionar existente
        for v in self.find_withtag("vector"):
            b = self.bbox(v)
            if b and (b[0]-5 <= cx <= b[2]+5) and (b[1]-5 <= cy <= b[3]+5):
                if cx > b[0]+10 and cx < b[2]-10 and cy > b[1]+10 and cy < b[3]-10: continue
                self.selected_vector_id, self.active_grip = self.gettags(v)[1], "move"
                self._drag_start_x, self._drag_start_y = cx, cy
                self._render(); return
                
        self.selected_vector_id, self.active_grip = None, None
        self._render(force_resize=False)

    def on_drag(self, event):
        if not self.selected_vector_id or not self.active_grip: return
        cx, cy = self.canvasx(event.x), self.canvasy(event.y)
        dx, dy = (cx - self._drag_start_x) / self.ratio, (cy - self._drag_start_y) / self.ratio
        self._drag_start_x, self._drag_start_y = cx, cy
        
        v = next((v for v in self.vectors if v["id"] == self.selected_vector_id), None)
        if not v: return
        
        c = v["coords"]
        if self.active_grip == "move":
            c[0]+=dx; c[1]+=dy; c[2]+=dx; c[3]+=dy
        elif self.active_grip in ["start", "tl"]: c[0]+=dx; c[1]+=dy
        elif self.active_grip in ["end", "br"]: c[2]+=dx; c[3]+=dy
        elif self.active_grip == "tr": c[2]+=dx; c[1]+=dy
        elif self.active_grip == "bl": c[0]+=dx; c[3]+=dy
        self._render(force_resize=False)

    def on_release(self, event):
        if self._is_new and self.selected_vector_id:
            v = next((v for v in self.vectors if v["id"] == self.selected_vector_id), None)
            if v and math.sqrt((v["coords"][2]-v["coords"][0])**2 + (v["coords"][3]-v["coords"][1])**2) < 15:
                self.vectors.remove(v); self.selected_vector_id = None
        
        self.active_grip = None
        self._render()
        self._save_vector_metadata()

    def delete_selected_vector(self, event=None):
        if self.selected_vector_id:
            self.vectors = [v for v in self.vectors if v["id"] != self.selected_vector_id]
            self.selected_vector_id = None
            self._render(); self._save_vector_metadata()

    def _is_point_in_bbox(self, x, y, bbox):
        return bbox and bbox[0]-3 <= x <= bbox[2]+3 and bbox[1]-3 <= y <= bbox[3]+3

    def _get_json_path(self):
        return os.path.splitext(self.image_path)[0] + ".json" if self.image_path else None

    def _save_vector_metadata(self):
        path = self._get_json_path()
        if path:
            try:
                with open(path, 'w') as f: json.dump(self.vectors, f)
            except: pass

    def _load_vector_metadata(self):
        path = self._get_json_path()
        if path and os.path.exists(path):
            try:
                with open(path, 'r') as f: self.vectors = json.load(f)
            except: self.vectors = []

    def get_composite_image(self):
        """Fusiona la imagen con los vectores en resolución nativa para exportar."""
        if not self.current_pil_image: return None
        comp = self.current_pil_image.copy()
        draw = ImageDraw.Draw(comp)
        for v in self.vectors:
            x1, y1, x2, y2 = v["coords"]
            w = max(5, int(constants.VECTOR_WIDTH / self.base_ratio))
            if v["type"] == "rect":
                draw.rectangle([min(x1,x2), min(y1,y2), max(x1,x2), max(y1,y2)], outline=v["color"], width=w)
            elif v["type"] == "arrow":
                draw.line([x1, y1, x2, y2], fill=v["color"], width=w)
                ang = math.atan2(y2 - y1, x2 - x1)
                wlen = constants.ARROW_WING_LEN / self.base_ratio
                for a in [-math.pi/6, math.pi/6]:
                    draw.line([x2, y2, x2 - wlen * math.cos(ang-a), y2 - wlen * math.sin(ang-a)], fill=v["color"], width=w)
        return comp

    def copy_to_clipboard(self):
        img = self.get_composite_image()
        return utils.copy_image_to_clipboard(img)
