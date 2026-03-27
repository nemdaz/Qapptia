import math
import tkinter as tk
from PIL import ImageDraw
from module_editor import constants

class BaseTool:
    """Clase base abstracta para todas las herramientas de dibujo."""
    def on_press(self, canvas, event, rx, ry): pass
    def on_drag(self, canvas, event, dx, dy): pass
    def on_release(self, canvas, event): pass
    
    def render(self, canvas, coords, color, width, zoom_level, ratio, img_x, img_y, v_id):
        """Renderiza el vector en el Canvas de Tkinter."""
        pass

    def render_native(self, draw, coords, color, width, base_ratio):
        """Renderiza el vector sobre una imagen PIL (Alta resolución)."""
        pass

class RectTool(BaseTool):
    """Estrategia para dibujo de rectángulos."""
    def render(self, canvas, coords, color, width, zoom_level, ratio, img_x, img_y, v_id):
        x1, y1, x2, y2 = coords
        px1, py1 = img_x + (x1 * ratio), img_y + (y1 * ratio)
        px2, py2 = img_x + (x2 * ratio), img_y + (y2 * ratio)
        
        pts = [px1, py1, px2, py1, px2, py2, px1, py2, px1, py1]
        canvas.create_line(pts, fill=color, width=width, capstyle=tk.ROUND, joinstyle=tk.ROUND, tags=("vector", v_id, "type_rect"))

    def render_native(self, draw, coords, color, width, base_ratio):
        x1, y1, x2, y2 = coords
        draw.rectangle([min(x1, x2), min(y1, y2), max(x1, x2), max(y1, y2)], outline=color, width=width)

class ArrowTool(BaseTool):
    """Estrategia para dibujo de flechas."""
    def render(self, canvas, coords, color, width, zoom_level, ratio, img_x, img_y, v_id):
        x1, y1, x2, y2 = coords
        px1, py1 = img_x + (x1 * ratio), img_y + (y1 * ratio)
        px2, py2 = img_x + (x2 * ratio), img_y + (y2 * ratio)
        
        # Cuerpo de la flecha
        canvas.create_line(px1, py1, px2, py2, fill=color, width=width, capstyle=tk.ROUND, joinstyle=tk.ROUND, tags=("vector", v_id, "type_arrow"))
        
        # Aletas
        ang = math.atan2(py2 - py1, px2 - px1)
        wlen = constants.ARROW_WING_LEN * zoom_level
        for a in [-math.pi/6, math.pi/6]:
            canvas.create_line(px2, py2, px2 - wlen * math.cos(ang-a), py2 - wlen * math.sin(ang-a), 
                             fill=color, width=width, capstyle=tk.ROUND, joinstyle=tk.ROUND, tags=("vector", v_id, "type_arrow"))

    def render_native(self, draw, coords, color, width, base_ratio):
        x1, y1, x2, y2 = coords
        draw.line([x1, y1, x2, y2], fill=color, width=width)
        
        ang = math.atan2(y2 - y1, x2 - x1)
        wlen = constants.ARROW_WING_LEN / base_ratio
        for a in [-math.pi/6, math.pi/6]:
            draw.line([x2, y2, x2 - wlen * math.cos(ang-a), y2 - wlen * math.sin(ang-a)], fill=color, width=width)

class HighlighterTool(BaseTool):
    """Estrategia para dibujo de resaltadores (Rectángulos rellenos con transparencia)."""
    def render(self, canvas, coords, color, width, zoom_level, ratio, img_x, img_y, v_id):
        x1, y1, x2, y2 = coords
        px1, py1 = img_x + (x1 * ratio), img_y + (y1 * ratio)
        px2, py2 = img_x + (x2 * ratio), img_y + (y2 * ratio)
        
        # Dimensiones del resaltador
        w = int(abs(px2 - px1))
        h = int(abs(py2 - py1))
        if w < 1 or h < 1: return
        
        # Imagen PIL con transparencia
        from module_editor import utils
        from PIL import ImageTk, Image
        r, g, b = utils.hex_to_rgb(color)
        
        # Obtener alpha paramétrico
        alpha = constants.HIGHLIGHTER_ALPHA
        
        overlay = Image.new("RGBA", (w, h), (r, g, b, alpha))
        tk_img = ImageTk.PhotoImage(overlay)
        
        # Caché para evitar garbage collection
        if hasattr(canvas, "_photo_cache"):
            canvas._photo_cache.append(tk_img)
        
        # Dibujar como imagen en el Canvas
        canvas.create_image(min(px1, px2), min(py1, py2), image=tk_img, anchor="nw",
                            tags=("vector", v_id, "type_highlighter"))

    def render_native(self, draw, coords, color, width, base_ratio):
        x1, y1, x2, y2 = coords
        from module_editor import utils
        r, g, b = utils.hex_to_rgb(color)
        alpha = constants.HIGHLIGHTER_ALPHA
        # Rectángulo relleno con transparencia Alpha en PIL
        draw.rectangle([min(x1, x2), min(y1, y2), max(x1, x2), max(y1, y2)], 
                       fill=(r, g, b, alpha))

class ToolDispatcher:
    """Gestor de estrategias de herramientas."""
    _tools = {
        "rect": RectTool(),
        "arrow": ArrowTool(),
        "highlighter": HighlighterTool()
    }
    
    @classmethod
    def get_tool(cls, tool_type):
        return cls._tools.get(tool_type)
