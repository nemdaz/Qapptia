import math

from PIL import ImageQt
from PySide6.QtWidgets import QGraphicsView, QGraphicsScene, QGraphicsItem, QGraphicsPixmapItem, QGraphicsTextItem
from PySide6.QtCore import Qt, QRectF, Signal, QTimer, QPointF
from PySide6.QtGui import QPixmap, QPen, QColor, QPainter, QPainterPath, QPainterPathStroker, QTextOption

from module_editor import constants
from module_editor.core import text_layout as text_support
from module_editor.core.annotation_renderer import DrawingTool


class VectorItem(QGraphicsItem):
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
            path.addRect(rect.adjusted(-tolerance["highlighter"], -tolerance["highlighter"],
                                       tolerance["highlighter"], tolerance["highlighter"]))
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


class InlineTextEditor(QGraphicsTextItem):
    def __init__(self, parent_item, vector_data, commit_callback):
        super().__init__(parent_item)
        self._vector_data = vector_data
        self._commit_callback = commit_callback
        self._original_text = vector_data.payload.get("text", "")
        self._finalized = False

        self.setPlainText(self._original_text)
        self.setTextInteractionFlags(Qt.TextEditorInteraction)
        self.setTabChangesFocus(True)
        self.setZValue(5)
        self.document().setDocumentMargin(0)
        text_option = self.document().defaultTextOption()
        text_option.setWrapMode(QTextOption.WordWrap)
        self.document().setDefaultTextOption(text_option)
        self.document().contentsChanged.connect(self._sync_layout)
        self._sync_layout()

    def _sync_layout(self):
        parent_item = self.parentItem()
        if parent_item is None:
            return
        font, _, content_rect = text_support.fit_text_qt(self.toPlainText(), parent_item.data.coords)
        self.setFont(font)
        self.setDefaultTextColor(QColor(self._vector_data.color))
        self.setPos(content_rect.topLeft())
        self.setTextWidth(max(1.0, content_rect.width()))

    def focusOutEvent(self, event):
        super().focusOutEvent(event)
        self._finalize(cancelled=False)

    def keyPressEvent(self, event):
        if event.key() == Qt.Key_Escape:
            self._finalize(cancelled=True)
            event.accept()
            return
        super().keyPressEvent(event)

    def _finalize(self, cancelled):
        if self._finalized:
            return
        self._finalized = True
        final_text = self._original_text if cancelled else self.toPlainText()
        self._commit_callback(final_text, cancelled)


class TextVectorItem(VectorItem):
    def __init__(self, data, commit_callback):
        super().__init__(data)
        self._commit_callback = commit_callback
        self._editor = None

    def paint(self, painter, option, widget=None):
        if not self.is_editing():
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
        if self.isSelected() or self.is_editing():
            self._paint_selection_bounds(painter)

    def set_coords(self, coords):
        super().set_coords(coords)
        if self._editor is not None:
            self._editor._sync_layout()

    def set_text_color(self, color):
        self.data.color = color
        if self._editor is not None:
            self._editor.setDefaultTextColor(QColor(color))
            self._editor._sync_layout()
        self.update()

    def is_editing(self):
        return self._editor is not None

    def start_editing(self):
        if self._editor is not None:
            return
        self._editor = InlineTextEditor(self, self.data, self._finish_edit)
        self.setSelected(True)
        QTimer.singleShot(0, self._focus_editor)

    def finish_editing(self, cancelled=False):
        if self._editor is None:
            return
        self._editor._finalize(cancelled)

    def _focus_editor(self):
        if self._editor is None:
            return
        self._editor.setFocus(Qt.OtherFocusReason)

    def _finish_edit(self, text, cancelled):
        if self.scene() is not None and self._editor is not None:
            self.scene().removeItem(self._editor)
        self._editor = None
        self._commit_callback(self, text, cancelled)

    def _paint_selection_bounds(self, painter):
        x1, y1, x2, y2 = self.data.coords
        rect = QRectF(min(x1, x2), min(y1, y2), abs(x2 - x1), abs(y2 - y1))
        painter.save()
        pen = QPen(QColor(constants.TEXT_STYLE["selection_border"]), constants.TEXT_STYLE["selection_border_width"])
        pen.setDashPattern(constants.TEXT_STYLE["selection_dash_pattern"])
        painter.setPen(pen)
        painter.setBrush(Qt.NoBrush)
        painter.drawRect(rect)
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

    def _start_text_drag(self, pos):
        self._dragging = {
            "item": None,
            "grip": "br",
            "last": pos,
            "created": True,
            "origin": (pos.x(), pos.y()),
            "pending_text": True,
        }

    def load_image(self, pil_image, path):
        self._document.load(pil_image, path)
        self.clear()
        pix = QPixmap.fromImage(ImageQt.ImageQt(pil_image))
        self.addItem(QGraphicsPixmapItem(pix))
        self.setSceneRect(QRectF(0, 0, pix.width(), pix.height()))
        
        for v in self._document.vectors:
            self.addItem(self._create_item(v))

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
        editing_item = self._find_editing_text_item()

        if isinstance(hit, InlineTextEditor):
            super().mousePressEvent(event)
            return

        if editing_item is not None and hit is not editing_item:
            editing_item.finish_editing()
            hit = self.itemAt(pos, self.views()[0].transform())
            handle_hit = self._find_handle_hit(pos)

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
            if self._draw_mode == "text":
                self._start_text_drag(pos)
                event.accept()
                return

            v = self._document.create_vector(self._draw_mode, pos, self._active_color)
            item = self._create_item(v)
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
            if self._dragging.get("pending_text") and self._dragging.get("item") is None:
                x1, y1 = self._dragging["origin"]
                distance = math.hypot(pos.x() - x1, pos.y() - y1)
                if distance < constants.TEXT_STYLE["create_min_distance"]:
                    self._dragging["last"] = pos
                    event.accept()
                    return

                origin = self._dragging["origin"]
                payload = {"text": ""}
                origin_pointf = QPointF(origin[0], origin[1])
                vector = self._document.create_vector("text", origin_pointf, self._active_color, payload=payload)
                item = self._create_item(vector)
                self.addItem(item)
                item.setSelected(True)
                item.set_coords([origin[0], origin[1], pos.x(), pos.y()])
                self._dragging["item"] = item
                self._dragging["pending_text"] = False
                self._dragging["last"] = pos
                event.accept()
                return

            delta = pos - self._dragging["last"]
            self._dragging["last"] = pos
            item = self._dragging["item"]
            if item is None:
                event.accept()
                return
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
            if self._dragging.get("pending_text") and self._dragging.get("item") is None:
                self._dragging = None
                event.accept()
                return

            dragged_item = self._dragging.get("item")
            if self._dragging.get("created"):
                item = dragged_item
                x1, y1 = self._dragging["origin"]
                x2, y2 = item.data.coords[2], item.data.coords[3]
                distance = math.hypot(x2 - x1, y2 - y1)
                min_distance = constants.TEXT_STYLE["create_min_distance"] if isinstance(item, TextVectorItem) else constants.VECTOR_STYLE["draw_min_distance"]
                if distance < min_distance:
                    self._document.delete_vector(item.data.shape_id)
                    self.removeItem(item)
                    self._dragging = None
                    event.accept()
                    return
                if isinstance(item, TextVectorItem):
                    rect = QRectF(min(item.data.coords[0], item.data.coords[2]), min(item.data.coords[1], item.data.coords[3]), abs(item.data.coords[2] - item.data.coords[0]), abs(item.data.coords[3] - item.data.coords[1]))
                    if rect.width() < constants.TEXT_STYLE["min_box_width"] or rect.height() < constants.TEXT_STYLE["min_box_height"]:
                        self._document.delete_vector(item.data.shape_id)
                        self.removeItem(item)
                        self._dragging = None
                        event.accept()
                        return
            self._dragging = None
            if isinstance(dragged_item, TextVectorItem):
                dragged_item.start_editing()
                event.accept()
                return
            self._persist_vectors()
        super().mouseReleaseEvent(event)

    def mouseDoubleClickEvent(self, event):
        hit = self.itemAt(event.scenePos(), self.views()[0].transform())
        if isinstance(hit, TextVectorItem):
            hit.start_editing()
            event.accept()
            return
        super().mouseDoubleClickEvent(event)

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
                if isinstance(item, TextVectorItem):
                    item.set_text_color(color)
                else:
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

    def _create_item(self, vector):
        if vector.shape_type == "text":
            return TextVectorItem(vector, self._handle_text_commit)
        return VectorItem(vector)

    def _find_editing_text_item(self):
        for item in self.items():
            if isinstance(item, TextVectorItem) and item.is_editing():
                return item
        return None

    def _handle_text_commit(self, item, text, cancelled):
        previous_text = item.data.payload.get("text", "")
        final_text = previous_text if cancelled else text.replace("\r\n", "\n")
        if not final_text.strip():
            self._document.delete_vector(item.data.shape_id)
            self.removeItem(item)
            self._persist_vectors()
            return

        item.data.payload["text"] = final_text
        self._document.update_vector_payload(item.data.shape_id, {"text": final_text})
        item.update()
        self._persist_vectors()

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
        return not isinstance(item, (VectorItem, InlineTextEditor))

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
