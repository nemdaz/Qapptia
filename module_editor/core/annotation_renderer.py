import math

from PySide6.QtCore import Qt, QRectF, QLineF
from PySide6.QtGui import QPen, QBrush, QColor, QPainter

from module_editor import constants
from module_editor.core import text_layout as text_support

class DrawingTool:
    @staticmethod
    def render_qt(painter, v_type, coords, color, width, payload=None):
        x1, y1, x2, y2 = coords
        q_color = QColor(color)
        pen = QPen(q_color, width)
        pen.setCapStyle(Qt.RoundCap)
        pen.setJoinStyle(Qt.RoundJoin)
        painter.setPen(pen)
        painter.setBrush(Qt.NoBrush)

        if v_type == "rect":
            painter.drawRoundedRect(QRectF(x1, y1, x2 - x1, y2 - y1), 2, 2)
        elif v_type == "arrow":
            dx, dy = x2 - x1, y2 - y1
            arrow_wing_len = constants.VECTOR_STYLE["arrow_wing_len"]
            if math.hypot(dx, dy) < max(arrow_wing_len * 0.35, width * 2):
                return
            painter.drawLine(QLineF(x1, y1, x2, y2))
            ang = math.atan2(dy, dx)
            wlen = arrow_wing_len
            for a in [-math.pi / 6, math.pi / 6]:
                wx = x2 - wlen * math.cos(ang - a)
                wy = y2 - wlen * math.sin(ang - a)
                painter.drawLine(QLineF(x2, y2, wx, wy))
        elif v_type == "highlighter":
            q_color.setAlpha(constants.HIGHLIGHTER_ALPHA)
            painter.setPen(Qt.NoPen)
            painter.setBrush(QBrush(q_color))
            painter.drawRect(QRectF(min(x1, x2), min(y1, y2), abs(x2 - x1), abs(y2 - y1)))
        elif v_type == "text":
            text = (payload or {}).get("text", "")
            font, _, content_rect = text_support.fit_text_qt(text, coords)
            document = text_support.create_qt_text_document(text_support.normalize_text(text), font, content_rect.width(), color=color)

            painter.save()
            painter.setRenderHint(QPainter.TextAntialiasing, True)
            painter.translate(content_rect.topLeft())
            painter.setClipRect(QRectF(0, 0, content_rect.width(), content_rect.height()))
            document.drawContents(painter)
            painter.restore()

    @staticmethod
    def render_pil(draw, v_type, coords, color, width, scale=1, payload=None):
        x1, y1, x2, y2 = (value * scale for value in coords)
        scaled_width = max(1, int(round(width * scale)))
        
        if v_type == "rect":
            DrawingTool._pil_draw_round_line(draw, [(x1, y1), (x2, y1), (x2, y2), (x1, y2), (x1, y1)], color, scaled_width)
        elif v_type == "arrow":
            dx, dy = x2 - x1, y2 - y1
            arrow_wing_len = constants.VECTOR_STYLE["arrow_wing_len"] * scale
            if math.hypot(dx, dy) < max(arrow_wing_len * 0.35, scaled_width * 2):
                return
            DrawingTool._pil_draw_round_line(draw, [(x1, y1), (x2, y2)], color, scaled_width)
            ang = math.atan2(dy, dx)
            wlen = arrow_wing_len
            for a in [-math.pi / 6, math.pi / 6]:
                wx, wy = x2 - wlen * math.cos(ang - a), y2 - wlen * math.sin(ang - a)
                DrawingTool._pil_draw_round_line(draw, [(x2, y2), (wx, wy)], color, scaled_width)
        elif v_type == "highlighter":
            r, g, b = int(color[1:3], 16), int(color[3:5], 16), int(color[5:7], 16)
            draw.rectangle([min(x1, x2), min(y1, y2), max(x1, x2), max(y1, y2)],
                           fill=(r, g, b, constants.HIGHLIGHTER_ALPHA))
        elif v_type == "text":
            text = (payload or {}).get("text", "")
            font, lines, padding = text_support.fit_text_pil(text, coords, scale=scale)
            line_spacing = text_support.get_pil_line_spacing(font)
            text_x = min(x1, x2) + padding
            text_y = min(y1, y2) + padding

            for line in lines:
                draw.text(
                    (text_x, text_y),
                    line,
                    font=font,
                    fill=color,
                )
                text_y += line_spacing

    @staticmethod
    def _pil_draw_round_line(draw, pts, color, width):
        # Pillow fallback for rounded joints
        draw.line(pts, fill=color, width=width, joint="curve")
        r = (width - 1) / 2
        for x, y in pts:
            draw.ellipse([x - r, y - r, x + r, y + r], fill=color)
