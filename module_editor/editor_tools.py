import math
import tkinter as tk
from PIL import ImageDraw
from module_editor import constants, utils

class BaseTool:
    # Clase base abstracta para todas las herramientas de dibujo.
    def on_press(self, canvas, event, rx, ry): pass
    def on_drag(self, canvas, event, dx, dy): pass
    def on_release(self, canvas, event): pass
    
    def render(self, canvas, coords, color, width, zoom_level, ratio, img_x, img_y, v_id):
        # Renderizado nativo ultra-rápido para interacción.
        pass

    def render_native(self, draw, coords, color, width, base_ratio):
        # Renderiza el vector sobre una imagen PIL.
        pass

    def _draw_round_line(self, draw, pts, color, width):
        # Dibuja una línea con extremos y uniones redondeadas.
        draw.line(pts, fill=color, width=width, joint="curve")
        r = (width - 1) / 2
        for x, y in pts:
            draw.ellipse([x-r, y-r, x+r, y+r], fill=color)

    def is_hit(self, px, py, coords, ratio, img_x, img_y, tolerance=10):
        # Detección de colisión matemática.
        return False


class RectTool(BaseTool):
    # Estrategia para dibujo de rectángulos.
    def render(self, canvas, coords, color, width, zoom_level, ratio, img_x, img_y, v_id):
        x1, y1, x2, y2 = coords
        px1, py1 = img_x + (x1 * ratio), img_y + (y1 * ratio)
        px2, py2 = img_x + (x2 * ratio), img_y + (y2 * ratio)
        pts = [px1, py1, px2, py1, px2, py2, px1, py2, px1, py1]
        canvas.create_line(pts, fill=color, width=width, capstyle=tk.ROUND, joinstyle=tk.ROUND, tags=("vector_preview", v_id))

    def render_native(self, draw, coords, color, width, base_ratio):
        x1, y1, x2, y2 = coords
        pts = [(x1, y1), (x2, y1), (x2, y2), (x1, y2), (x1, y1)]
        self._draw_round_line(draw, pts, color, width)

    def is_hit(self, px, py, coords, ratio, img_x, img_y, tolerance=10):
        # Detección transversal usando helper.
        x1, y1, x2, y2 = coords
        px1, py1 = img_x + (x1 * ratio), img_y + (y1 * ratio)
        px2, py2 = img_x + (x2 * ratio), img_y + (y2 * ratio)
        return utils.is_point_in_rect(px, py, px1, py1, px2, py2, tolerance, hollow=True)


class ArrowTool(BaseTool):
    # Estrategia para dibujo de flechas.

    def is_hit(self, px, py, coords, ratio, img_x, img_y, tolerance=10):
        # Detección transversal usando helper.
        x1, y1, x2, y2 = coords
        px1, py1 = img_x + (x1 * ratio), img_y + (y1 * ratio)
        px2, py2 = img_x + (x2 * ratio), img_y + (y2 * ratio)
        return utils.is_point_near_segment(px, py, px1, py1, px2, py2, tolerance)

    def render(self, canvas, coords, color, width, zoom_level, ratio, img_x, img_y, v_id):
        x1, y1, x2, y2 = coords
        px1, py1 = img_x + (x1 * ratio), img_y + (y1 * ratio)
        px2, py2 = img_x + (x2 * ratio), img_y + (y2 * ratio)
        canvas.create_line(px1, py1, px2, py2, fill=color, width=width, capstyle=tk.ROUND, joinstyle=tk.ROUND, tags=("vector_preview", v_id))
        ang = math.atan2(py2 - py1, px2 - px1)
        wlen = constants.ARROW_WING_LEN * zoom_level
        for a in [-math.pi/6, math.pi/6]:
            canvas.create_line(px2, py2, px2 - wlen * math.cos(ang-a), py2 - wlen * math.sin(ang-a), 
                             fill=color, width=width, capstyle=tk.ROUND, joinstyle=tk.ROUND, tags=("vector_preview", v_id))

    def render_native(self, draw, coords, color, width, base_ratio):
        x1, y1, x2, y2 = coords
        self._draw_round_line(draw, [(x1, y1), (x2, y2)], color, width)
        
        ang = math.atan2(y2 - y1, x2 - x1)
        wlen = constants.ARROW_WING_LEN / base_ratio
        for a in [-math.pi/6, math.pi/6]:
            wx, wy = x2 - wlen * math.cos(ang-a), y2 - wlen * math.sin(ang-a)
            self._draw_round_line(draw, [(x2, y2), (wx, wy)], color, width)

class HighlighterTool(BaseTool):
    # Estrategia para dibujo de resaltadores (Rectángulos con transparencia).

    def is_hit(self, px, py, coords, ratio, img_x, img_y, tolerance=10):
        # Detección transversal usando helper.
        x1, y1, x2, y2 = coords
        px1, py1 = img_x + (x1 * ratio), img_y + (y1 * ratio)
        px2, py2 = img_x + (x2 * ratio), img_y + (y2 * ratio)
        return utils.is_point_in_rect(px, py, px1, py1, px2, py2, tolerance, hollow=False)

    def render(self, canvas, coords, color, width, zoom_level, ratio, img_x, img_y, v_id):
        # Highlighter siempre usa su propia lógica (aunque no sea ultra-rápida)
        x1, y1, x2, y2 = coords
        px1, py1 = img_x + (x1 * ratio), img_y + (y1 * ratio)
        px2, py2 = img_x + (x2 * ratio), img_y + (y2 * ratio)
        w, h = int(abs(px2 - px1)), int(abs(py2 - py1))
        if w < 1 or h < 1: return
        from PIL import ImageTk, Image
        from module_editor import utils
        r, g, b = utils.hex_to_rgb(color)
        overlay = Image.new("RGBA", (w, h), (r, g, b, constants.HIGHLIGHTER_ALPHA))
        tk_img = ImageTk.PhotoImage(overlay)
        if hasattr(canvas, "_photo_cache"): canvas._photo_cache.append(tk_img)
        canvas.create_image(min(px1, px2), min(py1, py2), image=tk_img, anchor="nw", tags=("vector_preview", v_id))

    def render_native(self, draw, coords, color, width, base_ratio):
        x1, y1, x2, y2 = coords
        r, g, b = utils.hex_to_rgb(color)
        alpha = constants.HIGHLIGHTER_ALPHA
        # Rectángulo relleno con transparencia Alpha en PIL
        draw.rectangle([min(x1, x2), min(y1, y2), max(x1, x2), max(y1, y2)], 
                       fill=(r, g, b, alpha))

class ToolDispatcher:
    # Gestor de estrategias de herramientas.
    _tools = {
        "rect": RectTool(),
        "arrow": ArrowTool(),
        "highlighter": HighlighterTool()
    }
    
    @classmethod
    def get_tool(cls, tool_type):
        return cls._tools.get(tool_type)
