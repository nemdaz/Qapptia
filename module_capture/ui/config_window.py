import os

from PySide6.QtCore import QMetaObject, Qt, Slot
from PySide6.QtWidgets import QCheckBox, QDialog, QFileDialog, QFormLayout, QGridLayout, QGroupBox, QHBoxLayout, QLabel, QLineEdit, QMessageBox, QPushButton, QSlider, QSpinBox, QTabWidget, QVBoxLayout, QWidget

from module_capture import constants
from module_capture.application.capture_settings_service import capture_settings_service


class ShortcutLineEdit(QLineEdit):
    KEY_ALIASES = {
        Qt.Key_Control: "ctrl",
        Qt.Key_Shift: "shift",
        Qt.Key_Alt: "alt",
        Qt.Key_Meta: "windows",
    }

    def __init__(self, max_keys, initial_value="", parent=None):
        super().__init__(parent)
        self._max_keys = max_keys
        self._recorded_keys = []
        self._previous_value = initial_value.upper()
        self.setPlaceholderText(constants.WINDOW_TEXT["placeholders"]["shortcut"])
        self.setText(initial_value.upper())

    def focusInEvent(self, event):
        self._previous_value = self.text()
        self._recorded_keys = []
        self.clear()
        super().focusInEvent(event)

    def focusOutEvent(self, event):
        if not self.text().strip():
            self.setText(self._previous_value)
        super().focusOutEvent(event)

    def keyPressEvent(self, event):
        if event.key() in (Qt.Key_Tab, Qt.Key_Backtab):
            super().keyPressEvent(event)
            return

        if event.key() == Qt.Key_Backspace:
            self._recorded_keys = []
            self.clear()
            event.accept()
            return

        key_name = self._normalize_key(event)
        if not key_name:
            event.accept()
            return

        if key_name not in self._recorded_keys and len(self._recorded_keys) < self._max_keys:
            self._recorded_keys.append(key_name)
            self.setText("+".join(self._recorded_keys).upper())

        event.accept()

    def shortcut_value(self):
        return self.text().strip().lower()

    def _normalize_key(self, event):
        if event.key() in self.KEY_ALIASES:
            return self.KEY_ALIASES[event.key()]

        text = event.text().strip().lower()
        if text:
            return text

        key_int = int(event.key())
        if Qt.Key_A <= key_int <= Qt.Key_Z:
            return chr(key_int).lower()
        if Qt.Key_0 <= key_int <= Qt.Key_9:
            return chr(key_int)
        return ""


class CaptureConfigWindow(QDialog):
    def __init__(self, on_close_callback=None, parent=None):
        super().__init__(parent)
        self._on_close_callback = on_close_callback
        self._settings = capture_settings_service.load()

        self.setWindowTitle(constants.WINDOW_TEXT["title"])
        self.setModal(True)
        self.resize(constants.WINDOW_LAYOUT["width"], constants.WINDOW_LAYOUT["height"])

        self._build_ui()
        self._load_values()

    def _build_ui(self):
        layout = QVBoxLayout(self)
        layout.setContentsMargins(
            constants.WINDOW_LAYOUT["margin"],
            constants.WINDOW_LAYOUT["margin"],
            constants.WINDOW_LAYOUT["margin"],
            constants.WINDOW_LAYOUT["margin"],
        )
        layout.setSpacing(constants.WINDOW_LAYOUT["spacing"])

        self.tabs = QTabWidget()
        self.general_tab = QWidget()
        self.capture_tab = QWidget()
        self.tabs.addTab(self.general_tab, constants.WINDOW_TEXT["tabs"]["general"])
        self.tabs.addTab(self.capture_tab, constants.WINDOW_TEXT["tabs"]["captures"])

        self._build_general_tab()
        self._build_capture_tab()

        layout.addWidget(self.tabs)

        button_row = QHBoxLayout()
        button_row.addStretch(1)
        self.save_button = QPushButton(constants.WINDOW_TEXT["buttons"]["save_close"])
        self.save_button.clicked.connect(self._save_and_close)
        button_row.addWidget(self.save_button)
        layout.addLayout(button_row)

    def _build_general_tab(self):
        layout = QVBoxLayout(self.general_tab)

        path_row = QHBoxLayout()
        self.path_edit = QLineEdit()
        self.browse_button = QPushButton(constants.WINDOW_TEXT["buttons"]["browse"])
        self.browse_button.clicked.connect(self._browse_path)
        path_row.addWidget(self.path_edit, 1)
        path_row.addWidget(self.browse_button)

        form = QFormLayout()
        form.addRow(constants.WINDOW_TEXT["labels"]["save_path"], self._wrap_layout(path_row))

        filename_row = QHBoxLayout()
        self.filename_edit = QLineEdit()
        self.filename_edit.setPlaceholderText(constants.WINDOW_TEXT["placeholders"]["filename_format"])
        self.help_button = QPushButton(constants.WINDOW_TEXT["buttons"]["help"])
        self.help_button.setFixedWidth(constants.WINDOW_LAYOUT["help_button_width"])
        self.help_button.clicked.connect(self._show_format_help)
        filename_row.addWidget(self.filename_edit, 1)
        filename_row.addWidget(self.help_button)
        form.addRow(constants.WINDOW_TEXT["labels"]["filename_format"], self._wrap_layout(filename_row))

        quality_row = QHBoxLayout()
        self.quality_slider = QSlider(Qt.Horizontal)
        self.quality_slider.setRange(
            constants.CAPTURE_DEFAULTS["image_quality"]["min"],
            constants.CAPTURE_DEFAULTS["image_quality"]["max"],
        )
        self.quality_label = QLabel()
        self.quality_slider.valueChanged.connect(self._update_quality_label)
        quality_row.addWidget(self.quality_slider, 1)
        quality_row.addWidget(self.quality_label)
        form.addRow(constants.WINDOW_TEXT["labels"]["image_quality"], self._wrap_layout(quality_row))
        layout.addLayout(form)

        subfolders_group = QGroupBox(constants.WINDOW_TEXT["groups"]["subfolders"])
        subfolders_layout = QVBoxLayout(subfolders_group)
        self.month_check = QCheckBox(constants.WINDOW_TEXT["checkboxes"]["subfolder_month"])
        self.day_check = QCheckBox(constants.WINDOW_TEXT["checkboxes"]["subfolder_day"])
        self.hour_check = QCheckBox(constants.WINDOW_TEXT["checkboxes"]["subfolder_hour"])
        subfolders_layout.addWidget(self.month_check)
        subfolders_layout.addWidget(self.day_check)
        subfolders_layout.addWidget(self.hour_check)
        layout.addWidget(subfolders_group)

        cursor_group = QGroupBox(constants.WINDOW_TEXT["groups"]["cursor"])
        cursor_layout = QVBoxLayout(cursor_group)
        self.show_mouse_check = QCheckBox(constants.WINDOW_TEXT["checkboxes"]["show_mouse"])
        self.highlight_mouse_check = QCheckBox(constants.WINDOW_TEXT["checkboxes"]["highlight_mouse"])
        self.show_mouse_check.toggled.connect(self._on_show_mouse_toggled)
        cursor_layout.addWidget(self.show_mouse_check)
        cursor_layout.addWidget(self.highlight_mouse_check)
        layout.addWidget(cursor_group)
        layout.addStretch(1)

    def _build_capture_tab(self):
        layout = QVBoxLayout(self.capture_tab)
        layout.addWidget(self._build_screen_group())
        layout.addWidget(self._build_area_group())
        layout.addWidget(self._build_flow_group())
        layout.addStretch(1)

    def _build_screen_group(self):
        group = QGroupBox(constants.WINDOW_TEXT["groups"]["screen_mode"])
        grid = QGridLayout(group)
        self.shortcut_screen_edit = ShortcutLineEdit(3)
        self.timer_spin = QSpinBox()
        self.timer_spin.setRange(
            constants.CAPTURE_DEFAULTS["manual_timer"]["min"],
            constants.CAPTURE_DEFAULTS["manual_timer"]["max"],
        )
        self.timer_spin.setSuffix(constants.WINDOW_LAYOUT["timer_suffix"])
        grid.addWidget(QLabel(constants.WINDOW_TEXT["labels"]["shortcut"]), 0, 0)
        grid.addWidget(self.shortcut_screen_edit, 0, 1)
        grid.addWidget(QLabel(constants.WINDOW_TEXT["labels"]["timer"]), 1, 0)
        grid.addWidget(self.timer_spin, 1, 1)
        return group

    def _build_area_group(self):
        group = QGroupBox(constants.WINDOW_TEXT["groups"]["area_mode"])
        grid = QGridLayout(group)
        self.shortcut_area_edit = ShortcutLineEdit(3)
        grid.addWidget(QLabel(constants.WINDOW_TEXT["labels"]["shortcut"]), 0, 0)
        grid.addWidget(self.shortcut_area_edit, 0, 1)
        return group

    def _build_flow_group(self):
        group = QGroupBox(constants.WINDOW_TEXT["groups"]["flow_mode"])
        grid = QGridLayout(group)
        self.shortcut_flow_edit = ShortcutLineEdit(3)
        self.shortcut_pause_edit = ShortcutLineEdit(2)
        self.scroll_check = QCheckBox(constants.WINDOW_TEXT["checkboxes"]["enable_scroll_capture"])
        grid.addWidget(QLabel(constants.WINDOW_TEXT["labels"]["shortcut"]), 0, 0)
        grid.addWidget(self.shortcut_flow_edit, 0, 1)
        grid.addWidget(QLabel(constants.WINDOW_TEXT["labels"]["pause"]), 1, 0)
        grid.addWidget(self.shortcut_pause_edit, 1, 1)
        grid.addWidget(self.scroll_check, 2, 0, 1, 2)
        return group

    def _load_values(self):
        self.path_edit.setText(self._settings.save_path)
        self.filename_edit.setText(self._settings.filename_format)
        self.quality_slider.setValue(self._settings.image_quality)
        self.month_check.setChecked(self._settings.subfolder_month)
        self.day_check.setChecked(self._settings.subfolder_day)
        self.hour_check.setChecked(self._settings.subfolder_hour)
        self.show_mouse_check.setChecked(self._settings.show_mouse)
        self.highlight_mouse_check.setChecked(self._settings.highlight_mouse)
        self.highlight_mouse_check.setEnabled(self._settings.show_mouse)
        self.timer_spin.setValue(self._settings.manual_timer)
        self.shortcut_screen_edit.setText(self._settings.shortcut_screen.upper())
        self.shortcut_area_edit.setText(self._settings.shortcut_area.upper())
        self.shortcut_flow_edit.setText(self._settings.shortcut_flow.upper())
        self.shortcut_pause_edit.setText(self._settings.shortcut_flow_pause.upper())
        self.scroll_check.setChecked(self._settings.enable_scroll_capture)
        self._update_quality_label(self._settings.image_quality)

    def _browse_path(self):
        selected_path = QFileDialog.getExistingDirectory(self, "Selecciona carpeta", self.path_edit.text() or os.path.expanduser("~"))
        if selected_path:
            self.path_edit.setText(selected_path)

    def _show_format_help(self):
        QMessageBox.information(
            self,
            constants.WINDOW_TEXT["format_help"]["title"],
            constants.WINDOW_TEXT["format_help"]["body"],
        )

    def _update_quality_label(self, value):
        self.quality_label.setText(f"{value}%")

    def _on_show_mouse_toggled(self, enabled):
        self.highlight_mouse_check.setEnabled(enabled)
        if not enabled:
            self.highlight_mouse_check.setChecked(False)

    def _save_and_close(self):
        self._settings.save_path = self.path_edit.text().strip()
        self._settings.filename_format = self.filename_edit.text().strip() or constants.CAPTURE_DEFAULTS["filename_format"]
        self._settings.image_quality = int(self.quality_slider.value())
        self._settings.subfolder_month = self.month_check.isChecked()
        self._settings.subfolder_day = self.day_check.isChecked()
        self._settings.subfolder_hour = self.hour_check.isChecked()
        self._settings.show_mouse = self.show_mouse_check.isChecked()
        self._settings.highlight_mouse = self.highlight_mouse_check.isChecked() and self.show_mouse_check.isChecked()
        self._settings.manual_timer = int(self.timer_spin.value())
        self._settings.shortcut_screen = self.shortcut_screen_edit.shortcut_value() or constants.CAPTURE_DEFAULTS["shortcuts"]["screen"]
        self._settings.shortcut_area = self.shortcut_area_edit.shortcut_value() or constants.CAPTURE_DEFAULTS["shortcuts"]["area"]
        self._settings.shortcut_flow = self.shortcut_flow_edit.shortcut_value() or constants.CAPTURE_DEFAULTS["shortcuts"]["flow"]
        self._settings.shortcut_flow_pause = self.shortcut_pause_edit.shortcut_value() or constants.CAPTURE_DEFAULTS["shortcuts"]["flow_pause"]
        self._settings.enable_scroll_capture = self.scroll_check.isChecked()

        capture_settings_service.save(self._settings)

        if self._on_close_callback:
            self._on_close_callback()

        self.accept()

    def _wrap_layout(self, layout):
        container = QWidget()
        container.setLayout(layout)
        return container

    def request_wake_up(self):
        QMetaObject.invokeMethod(self, "_handle_wake_up", Qt.QueuedConnection)

    @Slot()
    def _handle_wake_up(self):
        self.setWindowState(self.windowState() & ~Qt.WindowMinimized)
        self.show()
        self.raise_()
        self.activateWindow()
