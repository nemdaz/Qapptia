import tkinter as tk
import json
import os
import math
from PIL import Image, ImageTk, ImageDraw
from module_editor import utils, constants

class VectorCanvas(tk.Canvas):
    def __init__(self, master, on_zoom_callback=None, **kwargs):
        super().__init__(master, bg="#1a1a1a", highlightthickness=0, **kwargs)
        self.master = master
        self.on_zoom_callback = on_zoom_callback
        self.img_item = None
        self.current_pil_image = None
        self.tk_image = None
        self.image_path = None
        
        # Estado geométrico y zoom
        self.zoom_level = 1.0 # 1.0 = Encajar en pantalla (Fit)
        self.base_ratio = 1.0 # Ratio calculado para "Fit"
        self.ratio = 1.0      # ratio final (base_ratio * zoom_level)
        self.img_x = 0
        self.img_y = 0
        self.vectors = [] # Lista de diccionarios con metadatos de los vectores
        
        # Interacción
        self.draw_mode = None
        self.selected_vector_id = None
        self.active_grip = None
        self._drag_start_x = 0
        self._drag_start_y = 0

        # Bindings
        self.bind("<Configure>", self.on_resize)
        self.bind("<ButtonPress-1>", self.on_press)
        self.bind("<B1-Motion>", self.on_drag)
        self.bind("<ButtonRelease-1>", self.on_release)
        
        # Zoom con Ctrl + Rueda
        self.bind("<Control-MouseWheel>", self.on_zoom)     # Windows
        self.bind("<Control-Button-4>", self.on_zoom)      # Linux scroll up
        self.bind("<Control-Button-5>", self.on_zoom)      # Linux scroll down

    def load_image(self, pil_image, path):
        self.current_pil_image = pil_image
        self.image_path = path
        self.vectors = []
        self.zoom_level = 1.0 # Reset zoom al cargar nueva imagen
        self._load_vector_metadata()
        self._render(force_resize=True)

    def _render(self, force_resize=True):
        if not self.current_pil_image:
            return
            
        canvas_w = self.winfo_width()
        canvas_h = self.winfo_height()
        
        if canvas_w < 10 or canvas_h < 10:
            return

        # Solo redimensionamos la imagen PIL si el tamaño del canvas cambió o es forzado (carga inicial)
        if force_resize or not hasattr(self, '_last_canvas_size') or self._last_canvas_size != (canvas_w, canvas_h):
            self._update_background_image(canvas_w, canvas_h)
            self._last_canvas_size = (canvas_w, canvas_h)
        
        self._redraw_vectors()

    def _update_background_image(self, canvas_w, canvas_h):
        img_w, img_h = self.current_pil_image.size
        
        # 1. Calcular ratio "Fit" (Base)
        ratio_w = canvas_w / img_w
        ratio_h = canvas_h / img_h
        self.base_ratio = min(ratio_w, ratio_h)
        if self.base_ratio > 1: self.base_ratio = 1
        
        # 2. Aplicar zoom dinámico
        self.ratio = self.base_ratio * self.zoom_level
        
        new_w, new_h = int(img_w * self.ratio), int(img_h * self.ratio)
        
        # 3. Calcular posición (Centrado si es más pequeño que el canvas)
        self.img_x = max((canvas_w - new_w) // 2, 0)
        self.img_y = max((canvas_h - new_h) // 2, 0)
        
        resized_img = self.current_pil_image.resize((new_w, new_h), Image.Resampling.LANCZOS)
        self.tk_image = ImageTk.PhotoImage(resized_img)
        
        self.delete("background")
        self.img_item = self.create_image(self.img_x, self.img_y, image=self.tk_image, anchor="nw", tags="background")
        self.tag_lower("background")
        
        # 4. Actualizar Scroll Region
        # Definimos el área desplazable como el tamaño de la imagen más un margen
        # Si la imagen es más pequeña que el canvas, el region coincide con el canvas para no draguear
        sr_w = max(new_w + self.img_x*2, canvas_w)
        sr_h = max(new_h + self.img_y*2, canvas_h)
        self.configure(scrollregion=(0, 0, sr_w, sr_h))

    def on_zoom(self, event):
        if not self.current_pil_image: return
        
        # 1. Coordenadas del mouse en el canvas antes del zoom
        cx = self.canvasx(event.x)
        cy = self.canvasy(event.y)
        
        # 2. Coordenadas relativas a la imagen (en pixeles reales)
        x_real = (cx - self.img_x) / self.ratio
        y_real = (cy - self.img_y) / self.ratio
        
        # 3. Determinar dirección y nuevo zoom
        old_zoom = self.zoom_level
        if event.num == 4 or event.delta > 0: # Scroll Up
            new_zoom = self.zoom_level * constants.ZOOM_STEP
        else: # Scroll Down
            new_zoom = self.zoom_level / constants.ZOOM_STEP
            
        new_zoom = max(constants.ZOOM_MIN, min(new_zoom, constants.ZOOM_MAX))
        
        if new_zoom != old_zoom:
            self.zoom_level = new_zoom
            # Re-renderiza (esto actualiza self.ratio y self.img_x/y)
            self._render(force_resize=True)
            
            # 4. Ajustar scroll para mantener el punto bajo el mouse
            # Nueva posición del punto en el canvas
            new_cx = self.img_x + (x_real * self.ratio)
            new_cy = self.img_y + (y_real * self.ratio)
            
            # Desplazamiento necesario
            dx = new_cx - cx
            dy = new_cy - cy
            
            # Mover el canvas
            sr = self.cget("scrollregion").split()
            sr_w, sr_h = float(sr[2]), float(sr[3])
            
            # xview_scroll pide unidades o páginas, xview_moveto pide fracción [0, 1]
            if sr_w > 0: self.xview_moveto((self.canvasx(0) + dx) / sr_w)
            if sr_h > 0: self.yview_moveto((self.canvasy(0) + dy) / sr_h)

            if self.on_zoom_callback:
                self.on_zoom_callback()
            
    def _render_full(self, event=None):
        self._render(force_resize=True)

    def on_resize(self, event):
        # Sincronía instantánea: renderizamos inmediatamente al detectar el cambio de tamaño
        self._render(force_resize=True)

    def set_draw_mode(self, mode):
        self.draw_mode = mode # 'rect', 'arrow', o None
        if mode:
            self.configure(cursor="crosshair")
        else:
            self.configure(cursor="")

    def _redraw_vectors(self):
        # Limpiar solo los elementos vectoriales y grips
        self.delete("vector")
        self.delete("grip")
        
        # Calcular grosor dinámico según el zoom para que no se vea "delgado" al ampliar
        v_width = max(1, int(constants.VECTOR_WIDTH * self.zoom_level))
        
        # Dibuja los vectores guardados escalándolos a la resolución de pantalla actual
        for v in self.vectors:
            x1, y1, x2, y2 = v["coords"]
            # Proyectar a pantalla
            px1 = self.img_x + (x1 * self.ratio)
            py1 = self.img_y + (y1 * self.ratio)
            px2 = self.img_x + (x2 * self.ratio)
            py2 = self.img_y + (y2 * self.ratio)
            
            if v["type"] == "rect":
                # Usamos create_line para un polígono cerrado porque soporta joinstyle=ROUND
                points = [px1, py1, px2, py1, px2, py2, px1, py2, px1, py1]
                self.create_line(points, fill=v["color"], width=v_width, 
                                 capstyle=tk.ROUND, joinstyle=tk.ROUND, tags=("vector", v["id"]))
            elif v["type"] == "arrow":
                # Dibujar cuerpo de flecha
                self.create_line(px1, py1, px2, py2, fill=v["color"], width=v_width, 
                                 capstyle=tk.ROUND, joinstyle=tk.ROUND, tags=("vector", v["id"]))
                
                # Calcular e inyectar aletas abiertas
                angle = math.atan2(py2 - py1, px2 - px1)
                wing_len = constants.ARROW_WING_LEN * self.zoom_level
                w1_x = px2 - wing_len * math.cos(angle - math.pi/6)
                w1_y = py2 - wing_len * math.sin(angle - math.pi/6)
                w2_x = px2 - wing_len * math.cos(angle + math.pi/6)
                w2_y = py2 - wing_len * math.sin(angle + math.pi/6)
                
                self.create_line(px2, py2, w1_x, w1_y, fill=v["color"], width=v_width, 
                                 capstyle=tk.ROUND, joinstyle=tk.ROUND, tags=("vector", v["id"]))
                self.create_line(px2, py2, w2_x, w2_y, fill=v["color"], width=v_width, 
                                 capstyle=tk.ROUND, joinstyle=tk.ROUND, tags=("vector", v["id"]))
                
            # Dibujar Grips si está seleccionado
            if self.selected_vector_id == v["id"]:
                self._draw_grips(px1, py1, px2, py2, v["id"], v["type"])

    def _draw_grips(self, x1, y1, x2, y2, v_id, v_type):
        r = 5
        if v_type == "rect":
            corners = [(x1, y1, "tl"), (x2, y1, "tr"), (x1, y2, "bl"), (x2, y2, "br")]
        else:
            corners = [(x1, y1, "start"), (x2, y2, "end")]
            
        for cx, cy, ctype in corners:
            self.create_rectangle(cx-r, cy-r, cx+r, cy+r, fill="white", outline="black", tags=("grip", f"grip_{ctype}_{v_id}"))

    # ================= FUNCIONES DE INTERACCIÓN =================
    def on_press(self, event):
        # Convertir coordenadas de pantalla a coordenadas de canvas (considerando scroll)
        cx = self.canvasx(event.x)
        cy = self.canvasy(event.y)
        
        if self.draw_mode:
            if not self.current_pil_image: return
            
            x_real = (cx - self.img_x) / self.ratio
            y_real = (cy - self.img_y) / self.ratio
            
            new_id = f"{self.draw_mode}_{len(self.vectors)}"
            new_vector = {
                "type": self.draw_mode,
                "id": new_id,
                "coords": [x_real, y_real, x_real, y_real],
                "color": "#00FF00"
            }
            self.vectors.append(new_vector)
            self.selected_vector_id = new_id
            self.active_grip = "br" if self.draw_mode == "rect" else "end"
            self._drag_start_x = event.x
            self._drag_start_y = event.y
            
            # Salimos del modo "creación" para que el movimiento sea de dibujo normal
            self.set_draw_mode(None)
            self._render(force_resize=False)
            return
            
        # Comprobar si tocó un grip
        grips = self.find_withtag("grip")
        for g in grips:
            if self._is_point_in_bbox(cx, cy, self.bbox(g)):
                tags = self.gettags(g)
                # grip_tl_rect_0
                self.active_grip = tags[1].split("_")[1] # tl, tr, bl, br
                self._drag_start_x = cx
                self._drag_start_y = cy
                return
                
        # Comprobar si tocó un vector (borde del rectángulo)
        vectors = self.find_withtag("vector")
        for v in vectors:
            bbox = self.bbox(v)
            # Aproximación gruesa para agarrar el borde
            if bbox and (bbox[0]-5 <= cx <= bbox[2]+5) and (bbox[1]-5 <= cy <= bbox[3]+5):
                """ 
                Para no complicar la mateática de si pinchó exactamente
                el borde (interior vacío), si está dentro del Bounding Box
                del rectángulo lo seleccionamos.
                """
                if cx > bbox[0]+10 and cx < bbox[2]-10 and cy > bbox[1]+10 and cy < bbox[3]-10:
                    continue # Tocó el hoyo interior transparente, ignorar
                
                tags = self.gettags(v)
                self.selected_vector_id = tags[1]
                self.active_grip = "move"
                self._drag_start_x = cx
                self._drag_start_y = cy
                self._render() # Dibujar grips
                return
                
        # Clic al vacío
        self.selected_vector_id = None
        self.active_grip = None
        self._render(force_resize=False)

    def on_drag(self, event):
        if not self.selected_vector_id or not self.active_grip: return
        
        cx = self.canvasx(event.x)
        cy = self.canvasy(event.y)
        
        dx_screen = cx - self._drag_start_x
        dy_screen = cy - self._drag_start_y
        
        # Convertir desplazamiento a pixeles reales de la imagen
        dx_real = dx_screen / self.ratio
        dy_real = dy_screen / self.ratio
        
        self._drag_start_x = cx
        self._drag_start_y = cy
        
        # Buscar el diccionario de este vector
        v_idx = next((i for i, v in enumerate(self.vectors) if v["id"] == self.selected_vector_id), None)
        if v_idx is None: return
        
        coords = self.vectors[v_idx]["coords"] # [x1, y1, x2, y2]
        
        if self.active_grip == "move":
            coords[0] += dx_real; coords[1] += dy_real
            coords[2] += dx_real; coords[3] += dy_real
        elif self.active_grip == "start" or self.active_grip == "tl":
            coords[0] += dx_real; coords[1] += dy_real
        elif self.active_grip == "end" or self.active_grip == "br":
            coords[2] += dx_real; coords[3] += dy_real
        elif self.active_grip == "tr":
            coords[2] += dx_real; coords[1] += dy_real
        elif self.active_grip == "bl":
            coords[0] += dx_real; coords[3] += dy_real
            
        self.vectors[v_idx]["coords"] = coords
        self._render(force_resize=False)

    def on_release(self, event):
        if self.active_grip:
            self.active_grip = None
            self._save_vector_metadata()

    def _is_point_in_bbox(self, x, y, bbox):
        if not bbox: return False
        return bbox[0]-3 <= x <= bbox[2]+3 and bbox[1]-3 <= y <= bbox[3]+3

    # ================= METADATA SIDECAR (.JSON) =================
    def _get_json_path(self):
        if not self.image_path: return None
        base, _ = os.path.splitext(self.image_path)
        return base + ".json"

    def _save_vector_metadata(self):
        json_path = self._get_json_path()
        if not json_path: return
        
        try:
            with open(json_path, 'w') as f:
                json.dump(self.vectors, f)
        except Exception as e:
            print(f"Error guardando json: {e}")

    def _load_vector_metadata(self):
        json_path = self._get_json_path()
        if not json_path or not os.path.exists(json_path): return
        
        try:
            with open(json_path, 'r') as f:
                self.vectors = json.load(f)
        except Exception as e:
            print(f"Error cargando json: {e}")
            self.vectors = []

    # ================= FUNCIONES DE EXPORTACIÓN =================
    def get_composite_image(self):
        """Genera una imagen PIL fusionando la original con los vectores en alta resolución."""
        if not self.current_pil_image: return None
        
        # Trabajamos sobre una copia para no alterar la original en memoria
        composite = self.current_pil_image.copy()
        draw = ImageDraw.Draw(composite)
        
        for v in self.vectors:
            raw_x1, raw_y1, raw_x2, raw_y2 = v["coords"]
            color = v["color"]
            
            # El grosor en la exportación debe ser proporcional para que se vea igual que en la vista "Fit"
            # Si el base_ratio es 0.1 (imagen 10x más grande que el canvas), el grosor debe ser 3 / 0.1 = 30
            width = max(5, int(constants.VECTOR_WIDTH / self.base_ratio))
            
            # Normalizar coordenadas para PIL
            x1, x2 = min(raw_x1, raw_x2), max(raw_x1, raw_x2)
            y1, y2 = min(raw_y1, raw_y2), max(raw_y1, raw_y2)
            
            if v["type"] == "rect":
                draw.rectangle([x1, y1, x2, y2], outline=color, width=width)
            elif v["type"] == "arrow":
                # En flechas usamos las coordenadas originales para mantener la dirección de la punta
                x1, y1, x2, y2 = raw_x1, raw_y1, raw_x2, raw_y2
                draw.line([x1, y1, x2, y2], fill=color, width=width)
                
                # Alas de la flecha (Proporcional a la resolución original)
                angle = math.atan2(y2 - y1, x2 - x1)
                wing_len = constants.ARROW_WING_LEN / self.base_ratio
                w1_x = x2 - wing_len * math.cos(angle - math.pi/6)
                w1_y = y2 - wing_len * math.sin(angle - math.pi/6)
                w2_x = x2 - wing_len * math.cos(angle + math.pi/6)
                w2_y = y2 - wing_len * math.sin(angle + math.pi/6)
                
                draw.line([x2, y2, w1_x, w1_y], fill=color, width=width)
                draw.line([x2, y2, w2_x, w2_y], fill=color, width=width)
                
        return composite

    def copy_to_clipboard(self):
        """Funde la imagen y la envía al portapapeles usando la utilidad."""
        img = self.get_composite_image()
        return utils.copy_image_to_clipboard(img)
