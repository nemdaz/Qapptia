import math

from PIL import ImageQt
from PySide6.QtWidgets import QGraphicsView, QGraphicsScene, QGraphicsPixmapItem
from PySide6.QtCore import Qt, QRectF, Signal, QTimer, QPointF
from PySide6.QtGui import QPixmap, QColor, QPainter, QCursor

from core.constants import INTERNAL_CONFIG
from module_editor import constants
from module_editor.ui.toolbar.canvas_item import CanvasItem
from module_editor.ui.toolbar.text_item import InlineTextEditor, TextCanvasItem

class ImageScene(QGraphicsScene):
    content_changed = Signal()
    selection_context_changed = Signal(object)

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

    def _create_text_item(self, start_pos, end_pos=None):
        payload = {"text": "", "font_px": constants.TEXT_STYLE["font_min_px"]}
        vector = self._document.create_vector("text", start_pos, self._active_color, payload=payload)
        item = self._create_item(vector)
        self.addItem(item)
        item.setSelected(True)

        if end_pos is None:
            end_pos = QPointF(
                start_pos.x() + INTERNAL_CONFIG["editor_tool_text_default_width"],
                start_pos.y() + INTERNAL_CONFIG["editor_tool_text_default_height"],
            )

        item.set_coords([start_pos.x(), start_pos.y(), end_pos.x(), end_pos.y()])
        item.recompute_font_px_to_fit()
        return item

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

    def has_selected_vectors(self):
        return any(isinstance(item, CanvasItem) for item in self.selectedItems())

    def selected_vector_color(self):
        for item in self.selectedItems():
            if isinstance(item, CanvasItem):
                return item.data.color
        return None

    def _emit_selection_context(self, context, color=None):
        self.selection_context_changed.emit({"context": context, "color": color})

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
            self._emit_selection_context("editing", item.data.color)
            self._dragging = {"item": item, "grip": grip_name, "last": pos}
            event.accept()
            return

        # 2. Body interaction
        if hit and isinstance(hit, CanvasItem):
            self.deselect_all()
            hit.setSelected(True)
            hit.update()
            self._emit_selection_context("editing", hit.data.color)
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
            self._emit_selection_context("drawing", item.data.color)
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
        self._emit_selection_context("none")
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
                origin_pointf = QPointF(origin[0], origin[1])
                item = self._create_text_item(origin_pointf, pos)
                self._emit_selection_context("drawing", item.data.color)
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
            if isinstance(item, TextCanvasItem) and grip != "move":
                item.recompute_font_px_to_fit()
            event.accept()
        else:
            super().mouseMoveEvent(event)

    def mouseReleaseEvent(self, event):
        if self._dragging:
            if self._dragging.get("pending_text") and self._dragging.get("item") is None:
                origin = self._dragging["origin"]
                origin_pointf = QPointF(origin[0], origin[1])
                item = self._create_text_item(origin_pointf)
                self._emit_selection_context("drawing", item.data.color)
                self._dragging = None
                QTimer.singleShot(0, item.start_editing)
                event.accept()
                return

            dragged_item = self._dragging.get("item")
            if self._dragging.get("created"):
                item = dragged_item
                x1, y1 = self._dragging["origin"]
                x2, y2 = item.data.coords[2], item.data.coords[3]
                distance = math.hypot(x2 - x1, y2 - y1)
                min_distance = constants.TEXT_STYLE["create_min_distance"] if isinstance(item, TextCanvasItem) else constants.VECTOR_STYLE["draw_min_distance"]
                if distance < min_distance:
                    self._document.delete_vector(item.data.shape_id)
                    self.removeItem(item)
                    self._emit_selection_context("none")
                    self._dragging = None
                    event.accept()
                    return
                if isinstance(item, TextCanvasItem):
                    rect = QRectF(min(item.data.coords[0], item.data.coords[2]), min(item.data.coords[1], item.data.coords[3]), abs(item.data.coords[2] - item.data.coords[0]), abs(item.data.coords[3] - item.data.coords[1]))
                    if rect.width() < constants.TEXT_STYLE["min_box_width"] or rect.height() < constants.TEXT_STYLE["min_box_height"]:
                        self._document.delete_vector(item.data.shape_id)
                        self.removeItem(item)
                        self._emit_selection_context("none")
                        self._dragging = None
                        event.accept()
                        return
            self._dragging = None
            if isinstance(dragged_item, TextCanvasItem):
                QTimer.singleShot(0, dragged_item.start_editing)
                event.accept()
                return
            self._persist_vectors()
        super().mouseReleaseEvent(event)

    def mouseDoubleClickEvent(self, event):
        hit = self.itemAt(event.scenePos(), self.views()[0].transform())
        if isinstance(hit, TextCanvasItem):
            hit.start_editing()
            event.accept()
            return
        super().mouseDoubleClickEvent(event)

    def vectors_changed(self):
        self._persist_vectors()

    def delete_selected(self):
        for item in list(self.selectedItems()):
            if isinstance(item, CanvasItem):
                self._document.delete_vector(item.data.shape_id)
                self.removeItem(item)
        self._persist_vectors()
        self._emit_selection_context("none")

    def recolor_selected(self, color):
        selected_items = [item for item in self.selectedItems() if isinstance(item, CanvasItem)]
        selected_types = {item.data.shape_type for item in selected_items}
        if not selected_items:
            return set()

        changed = False
        for item in selected_items:
            if isinstance(item, TextCanvasItem):
                item.set_text_color(color)
            else:
                item.data.color = color
                item.update()
            changed = self._document.update_vector_color(item.data.shape_id, color) or changed
        if changed:
            self._persist_vectors()
        return selected_types

    def get_composite_image(self):
        return self._document.get_composite_image()

    def _persist_vectors(self):
        self._document.save_vectors()
        self.content_changed.emit()

    def _find_handle_hit(self, pos):
        for item in self.items():
            if isinstance(item, CanvasItem) and item.isSelected():
                grip_name = item.handle_at(pos)
                if grip_name:
                    return item, grip_name
        return None

    def _create_item(self, vector):
        if vector.shape_type == "text":
            if "text" not in vector.payload or "font_px" not in vector.payload:
                raise ValueError(f"Invalid text payload for vector '{vector.shape_id}': required keys text,font_px")
            return TextCanvasItem(vector, self._handle_text_commit)
        return CanvasItem(vector)

    def _find_editing_text_item(self):
        for item in self.items():
            if isinstance(item, TextCanvasItem) and item.is_editing():
                return item
        return None

    def _handle_text_commit(self, item, text, cancelled, font_px):
        previous_text = item.data.payload.get("text", "")
        final_text = previous_text if cancelled else text.replace("\r\n", "\n")
        if not final_text.strip():
            self._document.delete_vector(item.data.shape_id)
            self.removeItem(item)
            self._persist_vectors()
            self._emit_selection_context("none")
            return

        item.data.payload["text"] = final_text
        item.data.payload["font_px"] = int(font_px)
        self._document.update_vector_payload(item.data.shape_id, {"text": final_text, "font_px": int(font_px)})
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
        self._draw_cursor_active = False

    def set_zoom_callback(self, cb): self._on_zoom_cb = cb

    def _set_canvas_cursor(self, cursor_shape):
        self.viewport().setCursor(cursor_shape)

    def _clear_canvas_cursor(self):
        self.viewport().unsetCursor()

    def set_draw_cursor_active(self, active):
        self._draw_cursor_active = bool(active)
        if self._pan_active:
            return
        self._update_draw_cursor(self.mapFromGlobal(QCursor.pos()))

    def _cursor_for_grip(self, grip_name):
        if grip_name in ("tl", "br"):
            return Qt.SizeFDiagCursor
        if grip_name in ("tr", "bl"):
            return Qt.SizeBDiagCursor
        if grip_name == "move":
            return Qt.SizeAllCursor
        if grip_name in ("start", "end"):
            return Qt.SizeFDiagCursor
        return Qt.CrossCursor

    def _update_draw_cursor(self, view_pos):
        if self._pan_active:
            return

        hit = self.itemAt(view_pos)
        if isinstance(hit, InlineTextEditor):
            self._set_canvas_cursor(Qt.IBeamCursor)
            return

        scene = self.scene()
        if scene is None:
            self._set_canvas_cursor(Qt.CrossCursor)
            return

        scene_pos = self.mapToScene(view_pos)
        handle_hit = None
        if hasattr(scene, "_find_handle_hit"):
            handle_hit = scene._find_handle_hit(scene_pos)

        if handle_hit:
            _, grip_name = handle_hit
            self._set_canvas_cursor(self._cursor_for_grip(grip_name))
            return

        if isinstance(hit, CanvasItem):
            self._set_canvas_cursor(Qt.SizeAllCursor)
            return

        if self._draw_cursor_active:
            self._set_canvas_cursor(Qt.CrossCursor)
        else:
            self._clear_canvas_cursor()

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
            self._set_canvas_cursor(Qt.ClosedHandCursor)
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
        self._update_draw_cursor(event.position().toPoint())

    def mouseReleaseEvent(self, event):
        if self._pan_active and event.button() == Qt.LeftButton:
            self._pan_active = False
            self._pan_last_pos = None
            self._update_draw_cursor(event.position().toPoint())
            event.accept()
            return
        super().mouseReleaseEvent(event)
        self._update_draw_cursor(event.position().toPoint())

    def _current_zoom_level(self):
        return self.transform().m11() or 1.0

    def _can_start_pan(self, view_pos):
        if not self._has_scroll_margin():
            return False

        item = self.itemAt(view_pos)
        return not isinstance(item, (CanvasItem, InlineTextEditor))

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
