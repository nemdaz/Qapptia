import os

from PIL import ImageQt
from PySide6.QtCore import QMetaObject, QSize, Qt, QTimer, Slot
from PySide6.QtGui import QColor, QIcon, QPainter, QPainterPath, QPen, QPixmap
from PySide6.QtWidgets import QApplication, QComboBox, QMainWindow, QSplitter, QToolBar, QVBoxLayout, QWidget

from core import ipc
from core.logger import logger
from module_editor import constants
from module_editor.core.editor_controller import EditorController
from module_editor.ui.canvas_view import CanvasView, ImageScene
from module_editor.ui.sidebar_tree import SidebarTree
from module_editor.widgets.notifications import show_toast


class ZoomComboBox(QComboBox):
    def paintEvent(self, event):
        super().paintEvent(event)
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing, True)
        painter.setPen(Qt.NoPen)
        painter.setBrush(QColor("#f0f0f0"))

        center_x = self.width() - 12
        center_y = self.height() // 2 + 1
        chevron = QPainterPath()
        chevron.moveTo(center_x - 4, center_y - 2)
        chevron.lineTo(center_x, center_y + 2)
        chevron.lineTo(center_x + 4, center_y - 2)
        chevron.closeSubpath()
        painter.drawPath(chevron)
        painter.end()


class MainWindow(QMainWindow):
    def __init__(self):
        super().__init__()
        self._controller = EditorController()
        self._color_btns = {}

        self.setWindowTitle(constants.WINDOW_TITLE)
        self._load_styles()
        self._resize_to_screen()

        self.current_color_name = self._controller.current_color_name
        self.current_color_hex = self._controller.current_color_hex

        self._setup_ui()
        ipc.start_ipc_server(self._wake_up, self._request_close_from_ipc)
        QTimer.singleShot(constants.INITIAL_LOAD_DELAY_MS, self._load_initial_image)

    def _load_styles(self):
        style_path = os.path.join(os.path.dirname(__file__), "..", "styles", "dark_theme.qss")
        if os.path.exists(style_path):
            with open(style_path, "r", encoding="utf-8") as f:
                self.setStyleSheet(f.read())

    def _resize_to_screen(self):
        screen_size = QApplication.primaryScreen().size()
        self.resize(int(screen_size.width() * 0.8), int(screen_size.height() * 0.8))

    def _setup_ui(self):
        central = QWidget()
        self.setCentralWidget(central)
        layout = QVBoxLayout(central)
        layout.setContentsMargins(10, 10, 10, 10)
        layout.setSpacing(0)

        layout.addWidget(self._create_toolbar())
        layout.addWidget(self._create_splitter(), 1)
        self.statusBar().showMessage("Listo")

    def _create_splitter(self):
        split = QSplitter(Qt.Horizontal)
        self.scene = ImageScene(self._controller.document)
        self.scene.set_active_color(self.current_color_hex)
        self.canvas = CanvasView(self.scene)
        self.canvas.set_zoom_callback(self._on_zoom_changed)
        split.addWidget(self.canvas)

        self.sidebar = SidebarTree()
        self.sidebar.image_selected.connect(self.show_image)
        self.sidebar.setMinimumWidth(constants.SIDEBAR_WIDTH)
        split.addWidget(self.sidebar)
        split.setStretchFactor(0, 1)
        split.setSizes([1000, constants.SIDEBAR_WIDTH])
        return split

    def _create_toolbar(self):
        toolbar = QToolBar()
        toolbar.setIconSize(QSize(20, 20))
        toolbar.setMovable(False)

        self.act_save = self._add_action(toolbar, "save", "save", self.save_rotation, enabled=False)
        self._add_action(toolbar, "rotate", "rotate", self.rotate_image)
        self._add_action(toolbar, "copy_file", "copy_file", self.copy_file_to_clipboard)
        self._add_action(toolbar, "copy_clipboard", "copy_clip", self.copy_to_clipboard)

        toolbar.addSeparator()
        self.zoom_combo = ZoomComboBox()
        self.zoom_combo.setFixedWidth(90)
        self.zoom_combo.setEditable(True)
        self.zoom_combo.lineEdit().setReadOnly(True)
        self.zoom_combo.lineEdit().setAlignment(Qt.AlignCenter)
        self.zoom_combo.addItems(constants.ZOOM_CONFIG["presets"])
        self.zoom_combo.setCurrentText("100%")
        self.zoom_combo.activated.connect(self._on_zoom_combo)
        toolbar.addWidget(self.zoom_combo)
        self._add_action(toolbar, "fit", "fit", self.fit_image)

        toolbar.addSeparator()
        self.act_arrow = self._add_action(toolbar, "arrow", "arrow", lambda: self.set_tool("arrow"), checkable=True)
        self.act_rect = self._add_action(toolbar, "rect", "rect", lambda: self.set_tool("rect"), checkable=True)
        self.act_high = self._add_action(toolbar, "highlighter", "highlighter", lambda: self.set_tool("highlighter"), checkable=True)

        toolbar.addSeparator()
        self._populate_color_actions(toolbar)
        return toolbar

    def _populate_color_actions(self, toolbar):
        for name, hex_val in constants.FAVORITE_COLORS.items():
            action = toolbar.addAction(self._make_color_icon(hex_val, name == self.current_color_name), "")
            action.setToolTip(f"{constants.TOOLTIPS['color_prefix']}{constants.FAVORITE_COLOR_NAMES.get(name, name)}")
            action.triggered.connect(lambda chk=False, n=name, h=hex_val: self.set_active_color(n, h))
            self._color_btns[name] = action

    def _add_action(self, toolbar, icon_name, tooltip_key, callback, enabled=True, checkable=False):
        from core import assets

        fn = getattr(assets, f"create_{icon_name}_icon")
        pix = QPixmap.fromImage(ImageQt.ImageQt(fn()))
        action = toolbar.addAction(QIcon(pix), "")
        action.setToolTip(constants.TOOLTIPS[tooltip_key])
        action.triggered.connect(callback)
        action.setEnabled(enabled)
        action.setCheckable(checkable)
        return action

    def _make_color_icon(self, hex_color, active=False):
        swatch = constants.COLOR_SWATCH_STYLE
        icon_size = swatch["icon_size"]
        pix = QPixmap(icon_size, icon_size)
        pix.fill(Qt.transparent)
        painter = QPainter(pix)
        painter.setRenderHint(QPainter.Antialiasing, True)

        outer_ring = QColor(swatch["outer_ring_active"] if active else swatch["outer_ring_inactive"])
        inner_ring = QColor(swatch["inner_ring_active"] if active else swatch["inner_ring_inactive"])
        color_fill = QColor(hex_color)
        padding = swatch["outer_padding"]

        painter.setBrush(Qt.NoBrush)
        painter.setPen(QPen(outer_ring, 2))
        painter.drawEllipse(padding, padding, icon_size - (padding * 2), icon_size - (padding * 2))

        painter.setPen(QPen(inner_ring, 2))
        painter.setBrush(color_fill)
        painter.drawEllipse(4, 4, icon_size - 8, icon_size - 8)
        painter.end()
        return QIcon(pix)

    def set_active_color(self, name, hex_val):
        self.current_color_name = name
        self.current_color_hex = hex_val
        self._controller.set_active_color(name)

        for color_name, action in self._color_btns.items():
            action.setIcon(self._make_color_icon(constants.FAVORITE_COLORS[color_name], color_name == name))

        self.scene.set_active_color(hex_val)
        self.scene.recolor_selected(hex_val)

    def set_tool(self, tool):
        active_tool = self._controller.set_active_tool(tool)
        tool = active_tool
        self.act_arrow.setChecked(tool == "arrow")
        self.act_rect.setChecked(tool == "rect")
        self.act_high.setChecked(tool == "highlighter")
        self.scene.set_draw_mode(tool)

    def show_image(self, path):
        if not os.path.exists(path):
            return

        try:
            display_image = self._controller.open_image(path)
            if display_image is None:
                return
            self.scene.load_image(display_image, self._controller.current_image_path)
            self.setWindowTitle(f"{constants.WINDOW_TITLE} - {os.path.basename(path)}")
            self.act_save.setEnabled(False)
            self.sidebar.select_path(self._controller.current_image_path)
            QTimer.singleShot(50, self.canvas.fit_to_scene)
        except Exception as exc:
            logger.error(f"Error show_image: {exc}")
            show_toast(self, "Error al abrir la imagen")

    def rotate_image(self):
        if not self._controller.current_image_path:
            return

        display_image = self._controller.rotate_image()
        self.scene.load_image(display_image, self._controller.current_image_path)
        self.act_save.setEnabled(True)
        show_toast(self, f"Rotado {self._controller.current_rotation} grados")

    def save_rotation(self):
        if not self._controller.has_unsaved_rotation:
            return

        try:
            display_image = self._controller.save_rotation()
            self.scene.load_image(display_image, self._controller.current_image_path)
            self.act_save.setEnabled(False)
            show_toast(self, "Imagen guardada")
        except Exception as exc:
            logger.error(f"Error save_rotation: {exc}")
            show_toast(self, "Error al guardar")

    def copy_to_clipboard(self):
        composite = self.scene.get_composite_image()
        if composite:
            QApplication.clipboard().setPixmap(QPixmap.fromImage(ImageQt.ImageQt(composite.convert("RGB"))))
            show_toast(self, "Imagen copiada")

    def copy_file_to_clipboard(self):
        image_path = self._controller.copy_file_path()
        if image_path:
            QApplication.clipboard().setText(image_path)
            show_toast(self, "Ruta copiada")

    def fit_image(self):
        self.canvas.fit_to_scene()

    def _on_zoom_changed(self):
        self.zoom_combo.setEditText(f"{int(round(self.canvas.zoom_level * 100))}%")

    def _on_zoom_combo(self, index):
        digits = "".join(c for c in self.zoom_combo.itemText(index) if c.isdigit())
        if digits:
            self.canvas.set_zoom_level(int(digits) / 100.0)

    def _load_initial_image(self):
        try:
            display_image = self._controller.restore_last_image()
            if display_image is None:
                return
            image_path = self._controller.current_image_path
            self.scene.load_image(display_image, image_path)
            self.setWindowTitle(f"{constants.WINDOW_TITLE} - {os.path.basename(image_path)}")
            self.act_save.setEnabled(False)
            self.sidebar.select_path(image_path)
            QTimer.singleShot(50, self.canvas.fit_to_scene)
        except Exception as exc:
            logger.error(f"Error restoring last image: {exc}")

    def _wake_up(self):
        QMetaObject.invokeMethod(self, "_handle_wake_up", Qt.QueuedConnection)

    @Slot()
    def _handle_wake_up(self):
        self.setWindowState(self.windowState() & ~Qt.WindowMinimized)
        self.show()
        self.raise_()
        self.activateWindow()
        self.sidebar.refresh_model()

    def _request_close_from_ipc(self):
        QMetaObject.invokeMethod(self, "close", Qt.QueuedConnection)

    def closeEvent(self, event):
        self._controller.close()
        super().closeEvent(event)
