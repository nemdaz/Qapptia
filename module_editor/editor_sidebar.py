import customtkinter as ctk
import tkinter as tk
import os
from module_editor import constants, state_manager
from module_editor.utils import Tooltip
from core.widgets import CTKSmoothScrollbar
from core import config, assets

class EditorSidebar(ctk.CTkFrame):
    """Explorador de archivos con persistencia de estado y navegación por teclado."""
    
    def __init__(self, master, on_image_selected_callback, **kwargs):
        super().__init__(master, **kwargs)
        self.on_image_selected = on_image_selected_callback
        
        # Persistencia de widgets para rendimiento (Lazy Loading)
        self.folder_buttons = {}    
        self.folder_containers = {} 
        self.file_buttons = {}      
        
        self.active_button = None
        self._has_images_cache = {}
        self._dir_items_cache = {}
        
        # Estado persistente
        self.state = state_manager.load_state()
        self.expanded_folders = set(self.state.get("expanded_folders", []))
        self.selected_path = self.state.get("last_selected_file")
        
        self.visible_items = [] 
        
        self._setup_ui()
        self.refresh_all()
        
        self.master.bind("<Up>", self._on_arrow_key)
        self.master.bind("<Down>", self._on_arrow_key)

    def _setup_ui(self):
        self.grid_rowconfigure(1, weight=1)
        self.grid_columnconfigure(0, weight=1)
        
        header = ctk.CTkFrame(self, fg_color="transparent")
        header.grid(row=0, column=0, sticky="ew", padx=10, pady=10)
        
        ctk.CTkLabel(header, text="Explorador", font=constants.FONT_HEADER).pack(side="left")
        
        btn_refresh = ctk.CTkButton(
            header, text="", image=assets.get_icon("refresh", size=constants.ICON_SIZE_SMALL),
            width=constants.BTN_REFRESH_SIZE, height=constants.BTN_REFRESH_SIZE, 
            fg_color="transparent", hover_color=constants.HIGHLIGHT_COLOR,
            command=self.refresh_all
        )
        btn_refresh.pack(side="right")
        Tooltip(btn_refresh, constants.TOOLTIPS["refresh"])
        
        self.tree_container = ctk.CTkFrame(self, fg_color="transparent")
        self.tree_container.grid(row=1, column=0, sticky="nsew", padx=5, pady=(0, 10))
        self.tree_container.grid_rowconfigure(0, weight=1)
        self.tree_container.grid_columnconfigure(0, weight=1)
        self.tree_container.grid_columnconfigure(1, weight=0, minsize=constants.SCROLL_RESERVE_WIDTH)
        
        # Canvas y scrollbar con anclaje de puntero (Smooth)
        self.tree_canvas = tk.Canvas(self.tree_container, bg=constants.BG_COLOR_DARK, highlightthickness=0)
        self.vsb = CTKSmoothScrollbar(self.tree_container, orientation="vertical", command=self.tree_canvas.yview)
        
        self.tree_canvas.configure(yscrollcommand=self.vsb.set)
        self.tree_canvas.grid(row=0, column=0, sticky="nsew")
        
        self.canvas_frame = ctk.CTkFrame(self.tree_canvas, fg_color=constants.BG_COLOR_DARK)
        self.tree_canvas_window = self.tree_canvas.create_window((0, 0), window=self.canvas_frame, anchor="nw")
        
        self.canvas_frame.bind("<Configure>", self._on_frame_configure)
        self.tree_canvas.bind("<Configure>", self._on_canvas_configure)
        self._bind_scroll_recursive(self)

    def refresh_all(self):
        """Reconstrucción total del árbol."""
        self._has_images_cache = {}
        self._dir_items_cache = {}
        for w in self.canvas_frame.winfo_children(): w.destroy()
        self.folder_buttons, self.folder_containers, self.file_buttons = {}, {}, {}
        
        base_path = os.path.expandvars(config.get("save_path"))
        if not os.path.exists(base_path): return
        
        self._build_tree_level(base_path, self.canvas_frame, level=0)
        self._update_navigation_list()
        if self.selected_path: self.highlight_path(self.selected_path)

    def _toggle_folder(self, path):
        path = path.replace("\\", "/")
        container, btn = self.folder_containers.get(path), self.folder_buttons.get(path)
        if not container: return

        if path in self.expanded_folders:
            self.expanded_folders.remove(path)
            container.pack_forget()
            if btn: btn.configure(image=assets.get_icon("folder_collapsed", size=constants.ICON_SIZE_SMALL))
            state_manager.update_expanded(path, False)
        else:
            self.expanded_folders.add(path)
            if not container.winfo_children():
                self._build_tree_level(path, container, int(getattr(container, "level", 0)))
            container.pack(fill="x")
            if btn: btn.configure(image=assets.get_icon("folder", size=constants.ICON_SIZE_SMALL))
            state_manager.update_expanded(path, True)
            
        self._update_navigation_list()

    def _build_tree_level(self, path, parent, level):
        items = self._get_sorted_items(path)
        for item in items:
            full_path = os.path.join(path, item).replace("\\", "/")
            if os.path.isdir(full_path):
                if not self._has_images(full_path): continue
                
                block = ctk.CTkFrame(parent, fg_color="transparent")
                block.pack(fill="x")
                
                is_expanded = full_path in self.expanded_folders
                icon = "folder" if is_expanded else "folder_collapsed"
                
                f_btn = ctk.CTkButton(
                    block, text="  " + item, font=constants.FONT_BOLD, height=constants.BTN_HEIGHT_SMALL,
                    image=assets.get_icon(icon, size=constants.ICON_SIZE_SMALL),
                    fg_color="transparent", anchor="w", hover_color=constants.HIGHLIGHT_COLOR,
                    command=lambda p=full_path: self._toggle_folder(p)
                )
                f_btn.pack(fill="x", padx=(level * constants.INDENT_SIZE + 5, 5), pady=(2, 0))
                self.folder_buttons[full_path] = f_btn
                
                container = ctk.CTkFrame(block, fg_color="transparent")
                container.level = level + 1
                self.folder_containers[full_path] = container
                
                for w in [block, f_btn, container]: w.bind("<MouseWheel>", self._on_mousewheel)
                if is_expanded:
                    container.pack(fill="x")
                    self._build_tree_level(full_path, container, level + 1)
            
            elif item.lower().endswith(('.png', '.jpg', '.jpeg')):
                btn = ctk.CTkButton(
                    parent, text=" " + item, image=assets.get_icon("image_file", size=constants.ICON_SIZE_SMALL),
                    fg_color="transparent", anchor="w", height=constants.BTN_HEIGHT_SMALL,
                    hover_color=constants.HIGHLIGHT_COLOR, command=lambda p=full_path: self._on_file_click(p)
                )
                btn.pack(fill="x", padx=(level * constants.INDENT_SIZE + 15, 5), pady=1)
                self.file_buttons[full_path] = btn
                btn.bind("<MouseWheel>", self._on_mousewheel)

    def _on_file_click(self, path):
        self.on_image_selected(path)
        self.highlight_path(path)

    def _update_navigation_list(self):
        """Actualiza la lista lógica de archivos visibles para navegación con flechas."""
        self.visible_items = []
        base = os.path.expandvars(config.get("save_path")).replace("\\", "/")
        self._scan_visible_logical(base)
        self._on_frame_configure()

    def _scan_visible_logical(self, path):
        for item in self._get_sorted_items(path):
            full_path = os.path.join(path, item).replace("\\", "/")
            if os.path.isdir(full_path):
                if full_path in self.expanded_folders: self._scan_visible_logical(full_path)
            elif item.lower().endswith(('.png', '.jpg', '.jpeg')):
                self.visible_items.append(full_path)

    def _on_arrow_key(self, event):
        if not self.visible_items: return
        try:
            idx = self.visible_items.index(self.selected_path) if self.selected_path in self.visible_items else -1
            new_idx = max(0, idx - 1) if event.keysym == "Up" else min(len(self.visible_items) - 1, idx + 1)
            if idx == -1: new_idx = 0
            self._on_file_click(self.visible_items[new_idx])
        except: pass

    def highlight_path(self, path):
        path = path.replace("\\", "/")
        if self.active_button and self.active_button.winfo_exists():
            self.active_button.configure(fg_color="transparent", font=constants.FONT_NORMAL)
        
        self.selected_path = path
        btn = self.file_buttons.get(path)
        if btn and btn.winfo_exists():
            self.active_button = btn
            btn.configure(fg_color=constants.HIGHLIGHT_COLOR, font=constants.FONT_BOLD)
        state_manager.set_last_selected(path)

    def _get_sorted_items(self, path):
        try:
            mtime = os.path.getmtime(path)
            if path in self._dir_items_cache:
                t, items = self._dir_items_cache[path]
                if mtime == t: return items
            items = sorted(os.listdir(path), key=lambda x: os.path.getmtime(os.path.join(path, x)), reverse=True)
            self._dir_items_cache[path] = (mtime, items)
            return items
        except: return []

    def _has_images(self, path):
        if path in self._has_images_cache: return self._has_images_cache[path]
        has = False
        try:
            for root, dirs, files in os.walk(path):
                if any(f.lower().endswith(('.png', '.jpg', '.jpeg')) for f in files):
                    has = True; break
        except: pass
        self._has_images_cache[path] = has
        return has

    def _on_frame_configure(self, event=None):
        self.update_idletasks()
        bbox = self.tree_canvas.bbox("all")
        if not bbox: return
        self.tree_canvas.configure(scrollregion=bbox)
        # Cálculo de visibilidad de scroll según desbordamiento de bbox
        if (bbox[3] - bbox[1]) > self.tree_canvas.winfo_height() + 5:
            self.vsb.grid(row=0, column=1, sticky="ns")
        else:
            self.vsb.grid_forget()
            self.tree_canvas.yview_moveto(0)

    def _on_canvas_configure(self, event):
        self.tree_canvas.itemconfig(self.tree_canvas_window, width=event.width)
        self.after(10, self._on_frame_configure)

    def _bind_scroll_recursive(self, widget):
        widget.bind("<MouseWheel>", self._on_mousewheel, add="+")
        for child in widget.winfo_children(): self._bind_scroll_recursive(child)
        
    def _on_mousewheel(self, event):
        if self.vsb.winfo_ismapped():
            self.tree_canvas.yview_scroll(int(-1 * (event.delta / 120)), "units")
