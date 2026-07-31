import os

from PIL import ImageQt
from PySide6.QtWidgets import QTreeView, QVBoxLayout, QWidget, QLabel, QPushButton, QHBoxLayout, QFileSystemModel
from PySide6.QtCore import QDir, Qt, Signal, QModelIndex, QSize, QTimer, QSortFilterProxyModel
from PySide6.QtGui import QFont, QIcon, QPixmap

from core import config, assets
from module_editor import constants, state_manager

IMAGE_EXTENSIONS = {".png", ".jpg", ".jpeg"}
HIDDEN_DIR_PREFIX = constants.ANNOTATION_DIR[:1]

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
                    if entry.is_dir() and entry.name.startswith(HIDDEN_DIR_PREFIX):
                        continue
                    if entry.is_dir() or os.path.splitext(entry.name)[1].lower() in IMAGE_EXTENSIONS:
                        return True
        except OSError:
            pass
        return False


class HiddenDirProxyModel(QSortFilterProxyModel):
    def filterAcceptsRow(self, source_row, source_parent):
        source_model = self.sourceModel()
        index = source_model.index(source_row, 0, source_parent)
        if source_model.isDir(index):
            name = source_model.fileName(index)
            if name.startswith(HIDDEN_DIR_PREFIX):
                return False
        return super().filterAcceptsRow(source_row, source_parent)

class SidebarTree(QWidget):
    image_selected = Signal(str)

    def __init__(self, parent=None):
        super().__init__(parent)
        self._suppress_selection_signal = False
        self._is_restoring_tree_state = False
        self._pending_preferred_path = None
        self._restore_timer = QTimer(self)
        self._restore_timer.setSingleShot(True)
        self._restore_timer.setInterval(120)
        self._restore_timer.timeout.connect(self._apply_tree_state)
        self._setup_ui()

    def _setup_ui(self):
        layout = QVBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(0)

        header = QWidget()
        header.setFixedHeight(36)
        header.setStyleSheet("background-color: #1a1a1a;")
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
            "QTreeView { background-color: #1a1a1a; border: none; }"
            "QTreeView::item { padding: 3px 4px; }"
        )
        self.tree.expanded.connect(self._on_expanded)
        self.tree.collapsed.connect(self._on_collapsed)
        layout.addWidget(self.tree, 1)

        self._model = None
        self._proxy = None
        self.refresh_model()

    def _to_source(self, proxy_index):
        return self._proxy.mapToSource(proxy_index) if self._proxy else proxy_index

    def _to_proxy(self, source_index):
        return self._proxy.mapFromSource(source_index) if self._proxy else source_index

    def refresh_model(self, preferred_path=None):
        self._pending_preferred_path = preferred_path or self.current_selected_path()
        self._is_restoring_tree_state = True
        base_path = os.path.expandvars(config.get("save_path"))
        if not os.path.isdir(base_path):
            self._is_restoring_tree_state = False
            return

        self._model = ImageFilterModel(self)
        self._model.directoryLoaded.connect(self._on_directory_loaded)
        self._model.setRootPath(base_path)
        self._model.setFilter(QDir.AllDirs | QDir.Files | QDir.NoDotAndDotDot)
        self._model.setNameFilters(["*.png", "*.jpg", "*.jpeg"])
        self._model.setNameFilterDisables(False)

        self._proxy = HiddenDirProxyModel(self)
        self._proxy.setSourceModel(self._model)

        self.tree.setModel(self._proxy)
        self.tree.setRootIndex(self._proxy.mapFromSource(self._model.index(base_path)))
        self.tree.sortByColumn(0, Qt.DescendingOrder)
        self.tree.selectionModel().currentChanged.connect(self._on_current_changed)

        for i in range(1, self._model.columnCount()):
            self.tree.hideColumn(i)

        self._schedule_tree_state_restore()

    def current_selected_path(self):
        if not self._model:
            return None

        index = self.tree.currentIndex()
        if not index.isValid():
            return None

        path = self._model.filePath(self._to_source(index))
        if os.path.isfile(path):
            return path.replace("\\", "/")
        return None

    def _normalize_compare_path(self, path):
        if not path:
            return None
        return os.path.normcase(os.path.normpath(path))

    def _on_current_changed(self, index, _previous):
        if self._suppress_selection_signal:
            return

        path = self._model.filePath(self._to_source(index))
        if os.path.isfile(path) and os.path.splitext(path)[1].lower() in IMAGE_EXTENSIONS:
            self.image_selected.emit(path.replace("\\", "/"))

    def select_path(self, path, expand_parents=True):
        if self._model and os.path.exists(path):
            current_path = self.current_selected_path()
            if (
                self._normalize_compare_path(current_path) == self._normalize_compare_path(path)
            ):
                return

            idx = self._to_proxy(self._model.index(os.path.normpath(path)))
            if not idx.isValid():
                return
            if expand_parents:
                self._expand_parent_chain(idx)
            self._suppress_selection_signal = True
            try:
                self.tree.setCurrentIndex(idx)
            finally:
                self._suppress_selection_signal = False
            self.tree.scrollTo(idx, QTreeView.EnsureVisible)

    def _schedule_tree_state_restore(self):
        if not self._is_restoring_tree_state:
            return
        self._restore_timer.start()

    def _on_directory_loaded(self, _path):
        self._schedule_tree_state_restore()

    def _apply_tree_state(self):
        if not self._is_restoring_tree_state:
            return

        self._restore_expanded_folders()
        preferred_path = self._pending_preferred_path
        self._pending_preferred_path = None
        self._is_restoring_tree_state = False
        if preferred_path and os.path.exists(preferred_path):
            self.select_path(preferred_path, expand_parents=False)

    def _restore_expanded_folders(self):
        if not self._model:
            return
        self.tree.collapseAll()
        state = state_manager.load_state()
        expanded = set(state.get("expanded_folders", []))
        sorted_paths = sorted(expanded, key=lambda p: p.count(os.sep))
        for folder_path in sorted_paths:
            if not os.path.isdir(folder_path):
                continue
            parent = os.path.dirname(folder_path)
            if parent not in expanded and parent != os.path.expandvars(config.get("save_path")):
                continue
            idx = self._to_proxy(self._model.index(folder_path))
            if idx.isValid():
                self.tree.expand(idx)

    def _on_expanded(self, index):
        if self._model:
            path = self._model.filePath(self._to_source(index))
            if os.path.isdir(path):
                state_manager.update_expanded(path, True)

    def _on_collapsed(self, index):
        if self._model:
            path = self._model.filePath(self._to_source(index))
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
