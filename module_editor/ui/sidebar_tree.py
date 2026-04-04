import os
from PIL import ImageQt
from PySide6.QtWidgets import QTreeView, QVBoxLayout, QWidget, QLabel, QPushButton, QHBoxLayout, QFileSystemModel
from PySide6.QtCore import QDir, Qt, Signal, QModelIndex, QSize
from PySide6.QtGui import QFont, QIcon, QPixmap

from core import config, assets
from module_editor import constants, state_manager

IMAGE_EXTENSIONS = {".png", ".jpg", ".jpeg"}

class ImageFilterModel(QFileSystemModel):
    def hasChildren(self, parent=QModelIndex()):
        if not parent.isValid():
            return super().hasChildren(parent)
        path = self.filePath(parent)
        if not os.path.isdir(path):
            return False
        try:
            with os.scandir(path) as it:
                for entry in it:
                    if entry.is_dir() or os.path.splitext(entry.name)[1].lower() in IMAGE_EXTENSIONS:
                        return True
        except OSError:
            pass
        return False

class SidebarTree(QWidget):
    image_selected = Signal(str)

    def __init__(self, parent=None):
        super().__init__(parent)
        self._setup_ui()

    def _setup_ui(self):
        layout = QVBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(0)

        header = QWidget()
        header.setFixedHeight(36)
        header.setStyleSheet("background-color: #2b2b2b; border-bottom: 1px solid #3a3a3a;")
        header_lay = QHBoxLayout(header)
        header_lay.setContentsMargins(10, 0, 5, 0)

        lbl = QLabel("Explorador")
        lbl.setFont(QFont("Arial", 9))
        lbl.setStyleSheet("color: #999; border: none; background: transparent;")
        header_lay.addWidget(lbl)
        header_lay.addStretch()

        btn_refresh = QPushButton()
        btn_refresh.setIcon(QIcon(QPixmap.fromImage(ImageQt.ImageQt(assets.create_refresh_icon()))))
        btn_refresh.setIconSize(QSize(16, 16))
        btn_refresh.setFixedSize(28, 28)
        btn_refresh.setToolTip(constants.TOOLTIPS["refresh"])
        btn_refresh.setStyleSheet(
            "QPushButton { background: transparent; border: 1px solid transparent; border-radius: 4px; }"
            "QPushButton:hover { background-color: #3a3a3a; border: 1px solid #4a4a4a; }"
        )
        btn_refresh.clicked.connect(self.refresh_model)
        header_lay.addWidget(btn_refresh)
        layout.addWidget(header)

        self.tree = QTreeView()
        self.tree.setHeaderHidden(True)
        self.tree.setAnimated(True)
        self.tree.setIndentation(15)
        self.tree.setStyleSheet(
            "QTreeView { background-color: #1e1e1e; border: 1px solid #3a3a3a; border-top: none; }"
            "QTreeView::item { padding: 3px 4px; }"
        )
        self.tree.clicked.connect(self._on_click)
        self.tree.expanded.connect(self._on_expanded)
        self.tree.collapsed.connect(self._on_collapsed)
        layout.addWidget(self.tree, 1)

        self._model = None
        self.refresh_model()

    def refresh_model(self):
        base_path = os.path.expandvars(config.get("save_path"))
        if not os.path.isdir(base_path):
            return

        self._model = ImageFilterModel()
        self._model.setRootPath(base_path)
        self._model.setFilter(QDir.AllDirs | QDir.Files | QDir.NoDotAndDotDot)
        self._model.setNameFilters(["*.png", "*.jpg", "*.jpeg"])
        self._model.setNameFilterDisables(False)

        self.tree.setModel(self._model)
        self.tree.setRootIndex(self._model.index(base_path))
        self.tree.sortByColumn(3, Qt.DescendingOrder)

        for i in range(1, self._model.columnCount()):
            self.tree.hideColumn(i)

        self._restore_expanded_folders()
        self.tree.scrollToTop()

    def _on_click(self, index):
        path = self._model.filePath(index)
        if os.path.isfile(path) and os.path.splitext(path)[1].lower() in IMAGE_EXTENSIONS:
            self.image_selected.emit(path.replace("\\", "/"))

    def select_path(self, path):
        if self._model and os.path.exists(path):
            idx = self._model.index(path.replace("/", os.sep))
            self.tree.setCurrentIndex(idx)

    def _restore_expanded_folders(self):
        if not self._model:
            return
        state = state_manager.load_state()
        for folder_path in state.get("expanded_folders", []):
            if os.path.isdir(folder_path):
                idx = self._model.index(folder_path)
                if idx.isValid():
                    self.tree.expand(idx)

    def _on_expanded(self, index):
        if self._model:
            path = self._model.filePath(index)
            if os.path.isdir(path):
                state_manager.update_expanded(path, True)

    def _on_collapsed(self, index):
        if self._model:
            path = self._model.filePath(index)
            if os.path.isdir(path):
                state_manager.update_expanded(path, False)

    def _expand_parent_chain(self, index):
        parent = index.parent()
        parents = []
        while parent.isValid():
            parents.append(parent)
            parent = parent.parent()
        for ancestor in reversed(parents):
            self.tree.expand(ancestor)
