import math

from PySide6.QtCore import Qt, QRectF, QTimer, QSignalBlocker, Signal
from PySide6.QtGui import QPen, QColor, QTextCursor, QTextOption, QIntValidator
from PySide6.QtWidgets import (
    QApplication,
    QGraphicsItem,
    QGraphicsTextItem,
    QGraphicsProxyWidget,
    QHBoxLayout,
    QLineEdit,
    QPushButton,
    QWidget,
)

from module_editor import constants
from module_editor.core import text_layout as text_support
from module_editor.core.annotation_renderer import DrawingTool
from module_editor.ui.toolbar.canvas_item import CanvasItem


class CanvasTextItem(CanvasItem):
    _PLACEHOLDER = constants.TEXT_STYLE["placeholder"]

    def __init__(self, data, commit_callback, size_change_callback):
        super().__init__(data)
        self._commit_callback = commit_callback
        self._size_change_callback = size_change_callback
        self._editor = None
        self._editing_start_coords = None

        self._size_control = _TextSizeControl(
            constants.TEXT_STYLE["font_min_px"],
            constants.TEXT_STYLE["font_max_px"],
        )
        self._size_control.setFixedSize(76, 22)
        self._size_control.setValue(int(self.data.payload["text_size"]))
        self._size_control.setFocusPolicy(Qt.NoFocus)
        self._size_control.setCursor(Qt.ArrowCursor)
        self._minus_btn = self._size_control._minus_btn
        self._plus_btn = self._size_control._plus_btn
        self._minus_btn.setFocusPolicy(Qt.NoFocus)
        self._plus_btn.setFocusPolicy(Qt.NoFocus)
        self._size_control.setStyleSheet(
            "QWidget#text-size-control {"
            "background-color: rgba(20, 20, 20, 175);"
            "border: none;"
            "}"
            "QPushButton#size-minus, QPushButton#size-plus {"
            "background-color: rgba(255, 255, 255, 32);"
            "color: white;"
            "border: none;"
            "font-weight: 700;"
            "}"
            "QPushButton#size-minus:hover, QPushButton#size-plus:hover {"
            "background-color: rgba(255, 255, 255, 50);"
            "}"
            "QPushButton#size-minus:pressed, QPushButton#size-plus:pressed {"
            "background-color: rgba(255, 255, 255, 70);"
            "}"
            "QLineEdit#size-value {"
            "background: transparent;"
            "border: none;"
            "color: white;"
            "padding: 0;"
            "}"
        )
        self._size_control.valueChanged.connect(self._on_size_control_changed)

        self._size_control_proxy = QGraphicsProxyWidget(self)
        self._size_control_proxy.setWidget(self._size_control)
        self._size_control_proxy.setFlag(QGraphicsItem.ItemIgnoresTransformations, True)
        self._size_control_proxy.setZValue(8)
        self._size_control_proxy.hide()

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
        self._sync_size_control_position()

    def ensure_text_height(self, required_height):
        x1, y1, x2, y2 = self.data.coords
        current_height = abs(y2 - y1)
        target_height = max(float(required_height), float(constants.TEXT_STYLE["min_box_height"]))
        if abs(target_height - current_height) <= 0.5:
            return

        new_y2 = y1 + target_height if y2 >= y1 else y1 - target_height
        self.set_coords([x1, y1, x2, new_y2])

    def recompute_text_size_to_fit(self):
        sample_text = self.data.payload["text"] or self._PLACEHOLDER
        self.data.payload["text_size"] = text_support.fit_text_size_to_fit(sample_text, self.data.coords)
        if self._editor is not None:
            self._editor.set_text_size(self.data.payload["text_size"])
        with QSignalBlocker(self._size_control):
            self._size_control.setValue(int(self.data.payload["text_size"]))
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
        self._editor = _InlineTextEditor(self, self.data, self._finish_edit)
        self.setSelected(True)
        self._sync_size_control_state()
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
        self._sync_size_control_state()
        self._commit_callback(self, text, cancelled, final_text_size)

    def itemChange(self, change, value):
        if change == QGraphicsItem.ItemSelectedHasChanged:
            self._sync_size_control_state()
        return super().itemChange(change, value)

    def _sync_size_control_state(self):
        visible = self.isSelected() or self.is_editing()
        self._size_control_proxy.setVisible(visible)
        with QSignalBlocker(self._size_control):
            self._size_control.setValue(int(self.data.payload["text_size"]))
        self._sync_size_control_position()

    def _sync_size_control_position(self):
        x1, y1, x2, y2 = self.data.coords
        right = max(x1, x2)
        top = min(y1, y2)
        control_w = self._size_control.width()
        control_h = self._size_control.height()
        self._size_control_proxy.setPos(right - control_w, top - control_h)

    def owns_size_control_item(self, item):
        current = item
        while current is not None:
            if current is self._size_control_proxy:
                return True
            current = current.parentItem()
        return False

    def is_point_on_size_control(self, scene_pos):
        if not self._size_control_proxy.isVisible():
            return False
        return self._size_control_proxy.sceneBoundingRect().contains(scene_pos)

    def is_point_inside(self, scene_pos):
        x1, y1, x2, y2 = self.data.coords
        rect = QRectF(min(x1, x2), min(y1, y2), abs(x2 - x1), abs(y2 - y1))
        return rect.contains(scene_pos)

    def is_point_on_item(self, scene_pos):
        return self.is_point_inside(scene_pos) or self.is_point_on_size_control(scene_pos)

    def _on_size_control_changed(self, value):
        self.data.payload["text_size"] = int(value)
        if self._editor is not None:
            self._editor.set_text_size(value)
        else:
            self._grow_text_box_for_current_content()
        self.update()
        self._size_change_callback(self, int(value))
        self._sync_size_control_position()

    def _grow_text_box_for_current_content(self):
        content_text = self.data.payload.get("text", "")
        normalized_text = text_support.normalize_text(content_text)
        font = text_support.build_qt_font(self.data.payload["text_size"])
        content_rect = text_support.get_content_rect(self.data.coords, text_support.get_text_padding(self.data.coords))
        document = text_support.create_qt_text_document(normalized_text, font, max(1.0, content_rect.width()))
        required_height = max(constants.TEXT_STYLE["min_box_height"], math.ceil(float(document.size().height())))
        self.ensure_text_height(required_height)

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


class _InlineTextEditor(QGraphicsTextItem):
    _PLACEHOLDER = constants.TEXT_STYLE["placeholder"]

    def __init__(self, parent_item, vector_data, commit_callback):
        super().__init__(parent_item)
        self._vector_data = vector_data
        self._commit_callback = commit_callback
        self._original_text = vector_data.payload.get("text", "")
        self._text_size = int(vector_data.payload["text_size"])
        self._finalized = False
        self._is_placeholder_active = not self._original_text or self._original_text == self._PLACEHOLDER

        if not self._is_placeholder_active:
            self.setPlainText(self._original_text)
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
        if not self._is_placeholder_active:
            self.setDefaultTextColor(QColor(self._vector_data.color))
        self.setPos(content_rect.topLeft())
        self.setTextWidth(max(1.0, content_rect.width()))
        if sync_height:
            self._sync_height_to_content()

    def _sync_height_to_content(self):
        parent_item = self.parentItem()
        if parent_item is None:
            return

        if self._is_placeholder_active:
            from PySide6.QtGui import QFontMetrics
            fm = QFontMetrics(self.font())
            required_height = max(
                constants.TEXT_STYLE["min_box_height"],
                float(fm.height()),
            )
        else:
            required_height = max(
                constants.TEXT_STYLE["min_box_height"],
                math.ceil(float(self.document().size().height())),
            )
        parent_item.ensure_text_height(required_height)

    def paint(self, painter, option, widget=None):
        if self._is_placeholder_active:
            painter.save()
            painter.setPen(QColor(160, 160, 160))
            painter.setFont(self.font())
            painter.drawText(self.boundingRect(), Qt.AlignLeft | Qt.AlignTop, self._PLACEHOLDER)
            painter.restore()
            return
        DrawingTool.draw_qt_text_shadows(
            painter,
            self._content_text(),
            self.font(),
            self.textWidth(),
        )
        super().paint(painter, option, widget)

    def _content_text(self):
        if self._is_placeholder_active:
            return ""
        return self.toPlainText()

    def focusOutEvent(self, event):
        super().focusOutEvent(event)
        parent_item = self.parentItem()
        if parent_item is not None and event.reason() == Qt.MouseFocusReason:
            from PySide6.QtGui import QCursor
            view = self.scene().views()[0] if self.scene() else None
            if view is not None:
                scene_pos = view.mapToScene(view.mapFromGlobal(QCursor.pos()))
                if parent_item.is_point_on_item(scene_pos):
                    return
        self._finalize(cancelled=False)

    def mousePressEvent(self, event):
        if self._is_placeholder_active:
            self._is_placeholder_active = False
            self.setDefaultTextColor(QColor(self._vector_data.color))
            self.update()
        super().mousePressEvent(event)

    def keyPressEvent(self, event):
        if self._is_placeholder_active:
            if event.key() == Qt.Key_Escape:
                self._finalize(cancelled=True)
                event.accept()
                return
            if event.key() in (Qt.Key_Backspace, Qt.Key_Delete):
                event.accept()
                return
            if event.text() and not event.text().isspace():
                self._is_placeholder_active = False
                self.setPlainText(event.text())
                self.setDefaultTextColor(QColor(self._vector_data.color))
                cursor = self.textCursor()
                cursor.movePosition(QTextCursor.End)
                self.setTextCursor(cursor)
                self._sync_layout(sync_height=True)
                event.accept()
                return
            event.ignore()
            return
        if event.key() == Qt.Key_Escape:
            self._finalize(cancelled=True)
            event.accept()
            return
        super().keyPressEvent(event)
        if not self.toPlainText() and not self._is_placeholder_active:
            self._is_placeholder_active = True
            self.update()
            self._sync_height_to_content()

    def _finalize(self, cancelled):
        if self._finalized:
            return
        self._finalized = True
        final_text = self._original_text if cancelled else self._content_text()
        self._commit_callback(final_text, cancelled)


class _TextSizeControl(QWidget):
    valueChanged = Signal(int)

    def __init__(self, min_value, max_value, parent=None):
        super().__init__(parent)
        self.setObjectName("text-size-control")
        self._min_value = int(min_value)
        self._max_value = int(max_value)
        self._value = self._min_value

        self._minus_btn = QPushButton("-")
        self._value_edit = QLineEdit()
        self._plus_btn = QPushButton("+")

        self._minus_btn.setObjectName("size-minus")
        self._value_edit.setObjectName("size-value")
        self._plus_btn.setObjectName("size-plus")

        self._minus_btn.setCursor(Qt.ArrowCursor)
        self._plus_btn.setCursor(Qt.ArrowCursor)
        self._value_edit.setAlignment(Qt.AlignCenter)
        self._value_edit.setFocusPolicy(Qt.ClickFocus)
        self._value_edit.setValidator(QIntValidator(self._min_value, self._max_value, self))
        self._value_edit.setMaxLength(len(str(self._max_value)))

        self._minus_btn.setFixedWidth(16)
        self._plus_btn.setFixedWidth(16)
        self._minus_btn.setFixedHeight(22)
        self._plus_btn.setFixedHeight(22)
        self._value_edit.setFixedHeight(22)

        layout = QHBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(0)
        layout.addWidget(self._minus_btn)
        layout.addWidget(self._value_edit, 1)
        layout.addWidget(self._plus_btn)

        self._minus_btn.clicked.connect(lambda: self._step_value(-1))
        self._plus_btn.clicked.connect(lambda: self._step_value(1))
        self._value_edit.editingFinished.connect(self._commit_typed_value)

    def value(self):
        return self._value

    def setValue(self, value):
        clamped = max(self._min_value, min(int(value), self._max_value))
        changed = clamped != self._value
        self._value = clamped
        self._value_edit.setText(str(self._value))
        if changed:
            self.valueChanged.emit(self._value)

    def _step_value(self, delta):
        self.setValue(self._value + int(delta))

    def _commit_typed_value(self):
        text_value = self._value_edit.text().strip()
        if not text_value:
            self._value_edit.setText(str(self._value))
            return
        self.setValue(int(text_value))
