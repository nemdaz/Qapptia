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
        self.document().contentsChanged.connect(self._sync_layout)
        self._sync_layout()

    def _sync_layout(self):
        parent_item = self.parentItem()
        if parent_item is None:
            return

        content_text = self._content_text()
        if content_text:
            font, _, content_rect = text_support.fit_text_qt(content_text, parent_item.data.coords)
        else:
            content_rect = text_support.get_content_rect(
                parent_item.data.coords,
                text_support.get_text_padding(parent_item.data.coords),
            )
            empty_font_px = max(
                constants.TEXT_STYLE["font_min_px"],
                min(constants.TEXT_STYLE["font_max_px"], int(max(1.0, content_rect.height() * 0.72))),
            )
            font = text_support.build_qt_font(empty_font_px)

        self.setFont(font)
        self.setDefaultTextColor(QColor(self._vector_data.color))
        self.setPos(content_rect.topLeft())
        self.setTextWidth(max(1.0, content_rect.width()))

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
        if self.scene() is not None:
            self.scene().setFocusItem(self._editor, Qt.OtherFocusReason)
        self._editor.setFocus(Qt.OtherFocusReason)
        cursor = self._editor.textCursor()
        cursor.movePosition(QTextCursor.End)
        self._editor.setTextCursor(cursor)

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