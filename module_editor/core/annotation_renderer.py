import math

from PySide6.QtCore import Qt, QRectF, QLineF
from PySide6.QtGui import QPen, QBrush, QColor, QPainter

from module_editor import constants
from module_editor.core import text_layout as text_support

class DrawingTool:
    @staticmethod
    def draw_qt_text_shadows(painter, text, font, width, top_left=None):
        normalized_text = text.replace("\r\n", "\n") if text else ""
        if not normalized_text.strip():
            return

        shadow_specs = (
            (constants.TEXT_STYLE["shadow_light_rgba"], constants.TEXT_STYLE["shadow_light_offsets"]),
            (constants.TEXT_STYLE["shadow_dark_rgba"], constants.TEXT_STYLE["shadow_dark_offsets"]),
        )

        for rgba, offsets in shadow_specs:
            shadow_document = text_support.create_qt_text_document(
                normalized_text,
                font,
                width,
                color=QColor(*rgba),
            )
            for dx, dy in offsets:
                painter.save()
                if top_left is not None:
                    painter.translate(top_left)
                painter.translate(dx, dy)
                shadow_document.drawContents(painter)
                painter.restore()

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
            rect = QRectF(min(x1, x2), min(y1, y2), abs(x2 - x1), abs(y2 - y1))
            painter.drawRoundedRect(rect, 2, 2)
        elif v_type == "line":
            painter.drawLine(QLineF(x1, y1, x2, y2))
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
            text_payload = payload or {}
            normalized_text = text_support.normalize_text(text_payload["text"])
            font = text_support.build_qt_font(text_payload["text_size"])
            content_rect = text_support.get_content_rect(coords, text_support.get_text_padding(coords))
            document = text_support.create_qt_text_document(normalized_text, font, content_rect.width(), color=color)

            painter.save()
            painter.setRenderHint(QPainter.TextAntialiasing, True)
            DrawingTool.draw_qt_text_shadows(painter, normalized_text, font, content_rect.width(), content_rect.topLeft())
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
        elif v_type == "line":
            DrawingTool._pil_draw_round_line(draw, [(x1, y1), (x2, y2)], color, scaled_width)
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
            text_payload = payload or {}
            normalized_text = text_support.normalize_text(text_payload["text"])
            font = text_support.load_pil_font(text_payload["text_size"])
            scaled_coords = [value * scale for value in coords]
            padding = text_support.get_text_padding(coords) * scale
            x1, y1, x2, y2 = scaled_coords
            content_width = max(1, abs(x2 - x1) - (padding * 2))
            lines = text_support.wrap_text_pil(normalized_text, font, content_width)
            line_spacing = text_support.get_pil_line_spacing(font)
            text_x = min(x1, x2) + padding
            text_y = min(y1, y2) + padding

            DrawingTool._draw_pil_text_shadows(draw, lines, font, text_x, text_y, line_spacing, scale)
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

    @staticmethod
    def _draw_pil_text_shadows(draw, lines, font, text_x, text_y, line_spacing, scale):
        shadow_specs = (
            (constants.TEXT_STYLE["shadow_light_rgba"], constants.TEXT_STYLE["shadow_light_offsets"]),
            (constants.TEXT_STYLE["shadow_dark_rgba"], constants.TEXT_STYLE["shadow_dark_offsets"]),
        )

        for rgba, offsets in shadow_specs:
            for dx, dy in offsets:
                dx_scaled = int(round(dx * scale))
                dy_scaled = int(round(dy * scale))
                line_y = text_y + dy_scaled
                for line in lines:
                    draw.text((text_x + dx_scaled, line_y), line, font=font, fill=rgba)
                    line_y += line_spacing
