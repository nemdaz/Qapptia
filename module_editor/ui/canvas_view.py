import math

from PIL import ImageQt
from PySide6.QtWidgets import QGraphicsView, QGraphicsScene, QGraphicsItem, QGraphicsPixmapItem
from PySide6.QtCore import Qt, QRectF, Signal
from PySide6.QtGui import QPixmap, QPen, QColor, QPainter, QPainterPath, QPainterPathStroker

from module_editor import constants
from module_editor.core.tools import DrawingTool

class VectorItem(QGraphicsItem):
    GRIP_NAMES = {"rect": ["tl", "tr", "bl", "br"], "highlighter": ["tl", "tr", "bl", "br"], "arrow": ["start", "end"]}

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
            path.addRect(rect.adjusted(-tolerance["highlighter"], -tolerance["highlighter"],
                                       tolerance["highlighter"], tolerance["highlighter"]))
            return path

        if self.data.shape_type == "rect":
            border_path = QPainterPath()
            border_path.addRect(rect)
            stroker = QPainterPathStroker()
            stroker.setWidth(max(stroke_width * 2 + tolerance["rect"], tolerance["rect"]))
            return stroker.createStroke(border_path)

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

class ImageScene(QGraphicsScene):
    content_changed = Signal()

    def __init__(self, document, parent=None):
        super().__init__(parent)
        self._document = document
        self.setBackgroundBrush(QColor(constants.UI_COLORS["canvas_bg"]))
        self._draw_mode = None
        self._active_color = constants.VECTOR_STYLE["default_color"]
        self._dragging = None

    def load_image(self, pil_image, path):
        self._document.load(pil_image, path)
        self.clear()
        pix = QPixmap.fromImage(ImageQt.ImageQt(pil_image))
        self.addItem(QGraphicsPixmapItem(pix))
        self.setSceneRect(QRectF(0, 0, pix.width(), pix.height()))
        
        for v in self._document.vectors:
            self.addItem(VectorItem(v))

    def set_draw_mode(self, mode):
        self._draw_mode = mode

    def set_active_color(self, color):
        self._active_color = color

    def deselect_all(self):
        for item in list(self.selectedItems()):
            item.setSelected(False)
        self.update()

    def mousePressEvent(self, event):
        pos = event.scenePos()
        handle_hit = self._find_handle_hit(pos)
        hit = self.itemAt(pos, self.views()[0].transform())

        if handle_hit:
            item, grip_name = handle_hit
            self.deselect_all()
            item.setSelected(True)
            item.update()
            self._dragging = {"item": item, "grip": grip_name, "last": pos}
            event.accept()
            return

        # 2. Body interaction
        if hit and isinstance(hit, VectorItem):
            self.deselect_all()
            hit.setSelected(True)
            hit.update()
            self._dragging = {"item": hit, "grip": "move", "last": pos}
            event.accept()
            return

        # 3. Draw mode
        if event.button() == Qt.LeftButton and self._draw_mode:
            self.deselect_all()
            v = self._document.create_vector(self._draw_mode, pos, self._active_color)
            item = VectorItem(v)
            self.addItem(item)
            item.setSelected(True)
            item.update()
            self._dragging = {
                "item": item,
                "grip": "br" if self._draw_mode != "arrow" else "end",
                "last": pos,
                "created": True,
                "origin": (pos.x(), pos.y()),
            }
            event.accept()
            return

        self.deselect_all()
        super().mousePressEvent(event)

    def mouseMoveEvent(self, event):
        if self._dragging:
            pos = event.scenePos()
            delta = pos - self._dragging["last"]
            self._dragging["last"] = pos
            item = self._dragging["item"]
            grip = self._dragging["grip"]
            coords = list(item.data.coords)
            
            if grip == "move":
                coords[0] += delta.x(); coords[1] += delta.y(); coords[2] += delta.x(); coords[3] += delta.y()
            elif grip in ("tl", "start"):
                coords[0] += delta.x(); coords[1] += delta.y()
            elif grip in ("br", "end"):
                coords[2] += delta.x(); coords[3] += delta.y()
            elif grip == "tr":
                coords[2] += delta.x(); coords[1] += delta.y()
            elif grip == "bl":
                coords[0] += delta.x(); coords[3] += delta.y()

            item.set_coords(coords)
            event.accept()
        else:
            super().mouseMoveEvent(event)

    def mouseReleaseEvent(self, event):
        if self._dragging:
            if self._dragging.get("created"):
                item = self._dragging["item"]
                x1, y1 = self._dragging["origin"]
                x2, y2 = item.data.coords[2], item.data.coords[3]
                distance = math.hypot(x2 - x1, y2 - y1)
                if distance < constants.VECTOR_STYLE["draw_min_distance"]:
                    self._document.delete_vector(item.data.shape_id)
                    self.removeItem(item)
                    self._dragging = None
                    event.accept()
                    return
            self._dragging = None
            self._persist_vectors()
        super().mouseReleaseEvent(event)

    def vectors_changed(self):
        self._persist_vectors()

    def delete_selected(self):
        for item in list(self.selectedItems()):
            if isinstance(item, VectorItem):
                self._document.delete_vector(item.data.shape_id)
                self.removeItem(item)
        self._persist_vectors()

    def recolor_selected(self, color):
        changed = False
        for item in list(self.selectedItems()):
            if isinstance(item, VectorItem):
                item.data.color = color
                item.update()
                changed = self._document.update_vector_color(item.data.shape_id, color) or changed
        if changed:
            self._persist_vectors()
        return changed

    def get_composite_image(self):
        return self._document.get_composite_image()

    def _persist_vectors(self):
        self._document.save_vectors()
        self.content_changed.emit()

    def _find_handle_hit(self, pos):
        for item in self.items():
            if isinstance(item, VectorItem) and item.isSelected():
                grip_name = item.handle_at(pos)
                if grip_name:
                    return item, grip_name
        return None

class CanvasView(QGraphicsView):
    def __init__(self, scene, parent=None):
        super().__init__(scene, parent)
        self.setRenderHints(QPainter.Antialiasing | QPainter.SmoothPixmapTransform)
        self.setDragMode(QGraphicsView.NoDrag)
        self.setTransformationAnchor(QGraphicsView.AnchorUnderMouse)
        self.setResizeAnchor(QGraphicsView.AnchorUnderMouse)
        self.zoom_level = 1.0
        self._on_zoom_cb = None
        self._pan_active = False
        self._pan_last_pos = None

    def set_zoom_callback(self, cb): self._on_zoom_cb = cb

    def wheelEvent(self, event):
        if event.modifiers() & Qt.ControlModifier:
            current_zoom = self._current_zoom_level()
            step = 0.1 if current_zoom < 1.0 else 0.25
            direction = 1 if event.angleDelta().y() > 0 else -1
            new_zoom = max(
                constants.ZOOM_CONFIG["min"],
                min(current_zoom + step * direction, constants.ZOOM_CONFIG["max"]),
            )
            if new_zoom != current_zoom:
                factor = new_zoom / current_zoom
                self.scale(factor, factor)
                self.zoom_level = new_zoom
                if self._on_zoom_cb:
                    self._on_zoom_cb()
            event.accept()
        else:
            super().wheelEvent(event)

    def set_zoom_level(self, zoom):
        current_zoom = self._current_zoom_level()
        if current_zoom <= 0:
            current_zoom = 1.0
        factor = zoom / current_zoom
        self.zoom_level = zoom
        self.scale(factor, factor)
        if self._on_zoom_cb:
            self._on_zoom_cb()

    def fit_to_scene(self):
        scene = self.scene()
        if not scene:
            return
        scene_rect = scene.sceneRect()
        if scene_rect.isNull() or scene_rect.isEmpty():
            return
        self.resetTransform()
        self.fitInView(scene_rect, Qt.KeepAspectRatio)
        self.zoom_level = self._current_zoom_level()
        if self._on_zoom_cb:
            self._on_zoom_cb()

    def mouseDoubleClickEvent(self, event):
        if event.button() == Qt.LeftButton and self._can_start_pan(event.position().toPoint()):
            self._pan_active = True
            self._pan_last_pos = event.position().toPoint()
            self.setCursor(Qt.ClosedHandCursor)
            event.accept()
            return
        super().mouseDoubleClickEvent(event)

    def mouseMoveEvent(self, event):
        if self._pan_active and self._pan_last_pos is not None:
            current_pos = event.position().toPoint()
            delta = current_pos - self._pan_last_pos
            self._pan_last_pos = current_pos
            self.horizontalScrollBar().setValue(self.horizontalScrollBar().value() - delta.x())
            self.verticalScrollBar().setValue(self.verticalScrollBar().value() - delta.y())
            event.accept()
            return
        super().mouseMoveEvent(event)

    def mouseReleaseEvent(self, event):
        if self._pan_active and event.button() == Qt.LeftButton:
            self._pan_active = False
            self._pan_last_pos = None
            self.unsetCursor()
            event.accept()
            return
        super().mouseReleaseEvent(event)

    def _current_zoom_level(self):
        return self.transform().m11() or 1.0

    def _can_start_pan(self, view_pos):
        if not self._has_scroll_margin():
            return False

        item = self.itemAt(view_pos)
        return not isinstance(item, VectorItem)

    def _has_scroll_margin(self):
        h_scroll = self.horizontalScrollBar()
        v_scroll = self.verticalScrollBar()
        return h_scroll.maximum() > h_scroll.minimum() or v_scroll.maximum() > v_scroll.minimum()

    def keyPressEvent(self, event):
        if event.key() == Qt.Key_Delete:
            scene = self.scene()
            if scene and hasattr(scene, "delete_selected"):
                scene.delete_selected()
                event.accept()
                return
        super().keyPressEvent(event)
