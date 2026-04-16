import math

from PySide6.QtCore import Qt, QRectF, QTimer
from PySide6.QtGui import QPen, QColor, QTextCursor, QTextOption
from PySide6.QtWidgets import QGraphicsItem, QGraphicsTextItem

from module_editor import constants
from module_editor.core import text_layout as text_support
from module_editor.core.annotation_renderer import DrawingTool
from module_editor.ui.toolbar.canvas_item import CanvasItem


class InlineTextEditor(QGraphicsTextItem):
    _EMPTY_PLACEHOLDER = " "

    def __init__(self, parent_item, vector_data, commit_callback):
        super().__init__(parent_item)
        self._vector_data = vector_data
        self._commit_callback = commit_callback
        self._original_text = vector_data.payload.get("text", "")
        self._text_size = int(vector_data.payload["text_size"])
        self._finalized = False

        self.setPlainText(self._original_text or self._EMPTY_PLACEHOLDER)
        self.setFlag(QGraphicsItem.ItemIsFocusable, True)
        self.setTextInteractionFlags(Qt.TextEditorInteraction)
        self.setTabChangesFocus(True)
        self.setZValue(5)
        self.document().setDocumentMargin(0)
        text_option = self.document().defaultTextOption()
        text_option.setWrapMode(QTextOption.WordWrap)
        self.document().setDefaultTextOption(text_option)
        self.document().contentsChanged.connect(self._sync_height_to_content)
        self._sync_layout(sync_height=True)

    @property
    def text_size(self):
        return self._text_size

    def set_text_size(self, text_size):
        self._text_size = int(text_size)
        self._sync_layout(sync_height=True)

    def _sync_layout(self, sync_height):
        parent_item = self.parentItem()
        if parent_item is None:
            return

        content_rect = text_support.get_content_rect(
            parent_item.data.coords,
            text_support.get_text_padding(parent_item.data.coords),
        )
        font = text_support.build_qt_font(self._text_size)

        self.setFont(font)
        self.setDefaultTextColor(QColor(self._vector_data.color))
        self.setPos(content_rect.topLeft())
        self.setTextWidth(max(1.0, content_rect.width()))
        if sync_height:
            self._sync_height_to_content()

    def _sync_height_to_content(self):
        parent_item = self.parentItem()
        if parent_item is None:
            return

        required_height = max(
            constants.TEXT_STYLE["min_box_height"],
            math.ceil(float(self.document().size().height())),
        )
        parent_item.ensure_text_height(required_height)

    def paint(self, painter, option, widget=None):
        DrawingTool.draw_qt_text_shadows(
            painter,
            self._content_text(),
            self.font(),
            self.textWidth(),
        )
        super().paint(painter, option, widget)

    def _content_text(self):
        text = self.toPlainText()
        return "" if text == self._EMPTY_PLACEHOLDER else text

    def focusOutEvent(self, event):
        super().focusOutEvent(event)
        self._finalize(cancelled=False)

    def keyPressEvent(self, event):
        if self.toPlainText() == self._EMPTY_PLACEHOLDER and event.text() and not event.text().isspace():
            self.setPlainText("")
            cursor = self.textCursor()
            cursor.movePosition(QTextCursor.End)
            self.setTextCursor(cursor)
        if event.key() == Qt.Key_Escape:
            self._finalize(cancelled=True)
            event.accept()
            return
        super().keyPressEvent(event)
        if not self.toPlainText():
            self.setPlainText(self._EMPTY_PLACEHOLDER)
            cursor = self.textCursor()
            cursor.movePosition(QTextCursor.End)
            self.setTextCursor(cursor)

    def _finalize(self, cancelled):
        if self._finalized:
            return
        self._finalized = True
        final_text = self._original_text if cancelled else self._content_text()
        self._commit_callback(final_text, cancelled)


class TextCanvasItem(CanvasItem):
    def __init__(self, data, commit_callback):
        super().__init__(data)
        self._commit_callback = commit_callback
        self._editor = None
        self._editing_start_coords = None

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
            self._editor._sync_layout(sync_height=False)

    def ensure_text_height(self, required_height):
        x1, y1, x2, y2 = self.data.coords
        current_height = abs(y2 - y1)
        target_height = max(float(required_height), current_height)
        if target_height <= current_height + 0.5:
            return

        new_y2 = y1 + target_height if y2 >= y1 else y1 - target_height
        self.set_coords([x1, y1, x2, new_y2])

    def recompute_text_size_to_fit(self):
        sample_text = self.data.payload["text"] or constants.TEXT_STYLE["placeholder"]
        self.data.payload["text_size"] = text_support.fit_text_size_to_fit(sample_text, self.data.coords)
        if self._editor is not None:
            self._editor.set_text_size(self.data.payload["text_size"])
        self.update()

    def set_text_color(self, color):
        self.data.color = color
        if self._editor is not None:
            self._editor.setDefaultTextColor(QColor(color))
            self._editor._sync_layout(sync_height=False)
        self.update()

    def is_editing(self):
        return self._editor is not None

    def start_editing(self):
        if self._editor is not None:
            return
        self._editing_start_coords = list(self.data.coords)
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
        if self.scene() is not None:
            self.scene().setFocusItem(self._editor, Qt.OtherFocusReason)
        self._editor.setFocus(Qt.OtherFocusReason)
        cursor = self._editor.textCursor()
        cursor.movePosition(QTextCursor.End)
        self._editor.setTextCursor(cursor)

    def _finish_edit(self, text, cancelled):
        final_text_size = self._editor.text_size if self._editor is not None else self.data.payload["text_size"]

        if cancelled and self._editing_start_coords is not None:
            self.set_coords(self._editing_start_coords)

        if self.scene() is not None and self._editor is not None:
            self.scene().removeItem(self._editor)
        self._editor = None
        self._editing_start_coords = None
        self._commit_callback(self, text, cancelled, final_text_size)

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