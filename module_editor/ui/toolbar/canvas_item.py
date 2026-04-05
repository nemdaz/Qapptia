from PySide6.QtCore import Qt, QRectF
from PySide6.QtGui import QPen, QColor, QPainterPath, QPainterPathStroker
from PySide6.QtWidgets import QGraphicsItem

from module_editor import constants
from module_editor.core.annotation_renderer import DrawingTool


class CanvasItem(QGraphicsItem):
    GRIP_NAMES = {"rect": ["tl", "tr", "bl", "br"], "highlighter": ["tl", "tr", "bl", "br"], "arrow": ["start", "end"], "text": ["tl", "tr", "bl", "br"]}

    def __init__(self, data):
        super().__init__()
        self.data = data
        self.setFlags(QGraphicsItem.ItemIsSelectable)

    def _handle_positions(self):
        x1, y1, x2, y2 = self.data.coords
        return {
            "tl": (x1, y1),
            "tr": (x2, y1),
            "bl": (x1, y2),
            "br": (x2, y2),
            "start": (x1, y1),
            "end": (x2, y2),
        }

    def handle_rect(self, name):
        positions = self._handle_positions()
        px, py = positions.get(name, (0, 0))
        grip_size = constants.VECTOR_STYLE["grip_size"]
        return QRectF(px - grip_size, py - grip_size, grip_size * 2, grip_size * 2)

    def handle_at(self, scene_pos):
        for name in self.GRIP_NAMES.get(self.data.shape_type, []):
            if self.handle_rect(name).contains(scene_pos):
                return name
        return None

    def set_coords(self, coords):
        self.prepareGeometryChange()
        self.data.coords = list(coords)
        self.update()

    def boundingRect(self):
        x1, y1, x2, y2 = self.data.coords
        stroke_width = constants.VECTOR_STYLE["stroke_width"]
        grip_size = constants.VECTOR_STYLE["grip_size"]
        padding = max(stroke_width * 2, grip_size * 2, 10)
        return QRectF(
            min(x1, x2) - padding,
            min(y1, y2) - padding,
            abs(x2 - x1) + padding * 2,
            abs(y2 - y1) + padding * 2,
        )

    def shape(self):
        x1, y1, x2, y2 = self.data.coords
        rect = QRectF(min(x1, x2), min(y1, y2), abs(x2 - x1), abs(y2 - y1))
        stroke_width = constants.VECTOR_STYLE["stroke_width"]
        tolerance = constants.VECTOR_STYLE["selection_tolerance"]

        if self.data.shape_type == "highlighter":
            path = QPainterPath()
            path.addRect(rect.adjusted(-tolerance["highlighter"], -tolerance["highlighter"], tolerance["highlighter"], tolerance["highlighter"]))
            return path

        if self.data.shape_type == "rect":
            border_path = QPainterPath()
            border_path.addRect(rect)
            stroker = QPainterPathStroker()
            stroker.setWidth(max(stroke_width * 2 + tolerance["rect"], tolerance["rect"]))
            return stroker.createStroke(border_path)

        if self.data.shape_type == "text":
            path = QPainterPath()
            text_tolerance = tolerance["text"]
            path.addRect(rect.adjusted(-text_tolerance, -text_tolerance, text_tolerance, text_tolerance))
            return path

        if self.data.shape_type == "arrow":
            line_path = QPainterPath()
            line_path.moveTo(x1, y1)
            line_path.lineTo(x2, y2)
            stroker = QPainterPathStroker()
            stroker.setWidth(max(stroke_width * 2 + tolerance["arrow"], tolerance["arrow"]))
            return stroker.createStroke(line_path)

        fallback = QPainterPath()
        fallback.addRect(rect)
        return fallback

    def paint(self, painter, option, widget=None):
        DrawingTool.render_qt(
            painter,
            self.data.shape_type,
            self.data.coords,
            self.data.color,
            constants.VECTOR_STYLE["stroke_width"],
            payload=self.data.payload,
        )
        if self.isSelected():
            self._paint_grips(painter)

    def _paint_grips(self, painter):
        grip_pen = QPen(QColor(Qt.black), 1)
        grip_brush = QColor(Qt.white)

        painter.save()
        painter.setPen(grip_pen)
        painter.setBrush(grip_brush)
        for name in self.GRIP_NAMES.get(self.data.shape_type, []):
            painter.drawRect(self.handle_rect(name))
        painter.restore()