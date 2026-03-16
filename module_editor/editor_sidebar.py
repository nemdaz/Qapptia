import customtkinter as ctk
import tkinter as tk
import os
from module_editor import constants
from module_editor.utils import Tooltip
from core import config
from core import assets

class EditorSidebar(ctk.CTkFrame):
    def __init__(self, master, on_image_selected_callback, **kwargs):
        super().__init__(master, **kwargs)
        self.on_image_selected = on_image_selected_callback
        self.file_buttons = {}  # Mapeo de full_path -> CTkButton
        self.selected_path = None
        self.active_button = None # Referencia al botón seleccionado actualmente
        
        self.grid_rowconfigure(1, weight=1)
        self.grid_columnconfigure(0, weight=1)
        
        # Encabezado
        header_frame = ctk.CTkFrame(self, fg_color="transparent")
        header_frame.grid(row=0, column=0, sticky="ew", padx=10, pady=10)
        
        lbl_title = ctk.CTkLabel(header_frame, text="Explorador", font=("Arial", 14, "bold"))
        lbl_title.pack(side="left")
        
        icon_refresh = assets.get_icon("refresh", size=(16, 16))
        btn_refresh = ctk.CTkButton(header_frame, text="", image=icon_refresh, width=30, height=30, command=self.populate_file_tree)
        btn_refresh.pack(side="right")
        Tooltip(btn_refresh, "Recargar Archivos")
        
        # Área de Scroll 2D
        self.tree_container = ctk.CTkFrame(self, fg_color="transparent")
        self.tree_container.grid(row=1, column=0, sticky="nsew", padx=5, pady=(0, 10))
        
        self.tree_container.grid_rowconfigure(0, weight=1)
        self.tree_container.grid_columnconfigure(0, weight=1)
        
        self.tree_canvas = tk.Canvas(self.tree_container, bg="#2b2b2b", highlightthickness=0)
        self.vsb = ctk.CTkScrollbar(self.tree_container, orientation="vertical", command=self.tree_canvas.yview)
        # Scroll horizontal se inicializa pero no se dibuja (grid_forget)
        self.hsb = ctk.CTkScrollbar(self.tree_container, orientation="horizontal", command=self.tree_canvas.xview)
        
        self.tree_canvas.configure(yscrollcommand=self.vsb.set, xscrollcommand=self.hsb.set)
        
        self.tree_canvas.grid(row=0, column=0, sticky="nsew")
        self.vsb.grid(row=0, column=1, sticky="ns")
        
        self.tree_scrollable_frame = ctk.CTkFrame(self.tree_canvas, fg_color="#2b2b2b")
        self.tree_canvas_window = self.tree_canvas.create_window((0, 0), window=self.tree_scrollable_frame, anchor="nw")
        
        self.tree_scrollable_frame.bind("<Configure>", self._on_frame_configure)
        self.tree_canvas.bind("<Configure>", self._on_canvas_configure)
        
        self.populate_file_tree()
        self._canvas_configure_timeout = None
        
        # Vincular scroll a los contenedores base de forma recursiva
        self._bind_scroll_recursive(self)
        
    def _bind_scroll_recursive(self, widget):
        """Vincula el scroll de forma recursiva a un widget y sus hijos."""
        # Solo vincular si no es un widget que ya maneja su propio scroll o si es el canvas
        widget.bind("<MouseWheel>", self._on_mousewheel, add="+")
        for child in widget.winfo_children():
            self._bind_scroll_recursive(child)
        
    def _on_mousewheel(self, event):
        # Si se está presionando Control, ignoramos para dejar que el Canvas haga zoom
        if event.state & 0x0004: # 0x0004 es el bit de Control en Windows
            return
            
        # Desplazamiento reactivo asumiendo deltas típicos de Windows de +-120
        self.tree_canvas.yview_scroll(int(-1 * (event.delta / 120)), "units")
        
    def _on_frame_configure(self, event=None):
        self.tree_canvas.configure(scrollregion=self.tree_canvas.bbox("all"))
        self._check_scrollbars()
        
    def _on_canvas_configure(self, event):
        # Debouncing del lienzo (Canvas) principal para evitar "Flickering" que blanquea la UI en CustomTkinter
        self._last_event_width = event.width
        if self._canvas_configure_timeout:
            self.after_cancel(self._canvas_configure_timeout)
        self._canvas_configure_timeout = self.after(constants.DEBOUNCE_DELAY_MS, self._apply_canvas_configure)

    def _apply_canvas_configure(self):
        # Ajustamos el ancho interior del canvas para que al menos tome el tamaño de la ventana
        req_width = self.tree_scrollable_frame.winfo_reqwidth()
        self.tree_canvas.itemconfig(self.tree_canvas_window, width=max(self._last_event_width, req_width))
        self._check_scrollbars()
        
    def _check_scrollbars(self):
        bbox = self.tree_canvas.bbox("all")
        if bbox:
            canvas_width = self.tree_canvas.winfo_width()
            frame_width = bbox[2] - bbox[0]
            
            # Solo mostrar en el Grid inferior si el ancho del contenido es mayor al de la pantalla
            if frame_width > canvas_width and canvas_width > 10:
                self.hsb.grid(row=1, column=0, sticky="ew")
            else:
                self.hsb.grid_forget()

    def populate_file_tree(self):
        self.file_buttons = {}
        self.active_button = None
        for widget in self.tree_scrollable_frame.winfo_children():
            widget.destroy()
            
        base_path = os.path.expandvars(config.get("save_path"))
        if not os.path.exists(base_path):
            ctk.CTkLabel(self.tree_scrollable_frame, text="Ruta no encontrada.", text_color="gray").pack(pady=10)
            return

        self._build_tree_level(base_path, level=0)
        
        # Después de cargar todo, vinculamos el scroll a los nuevos botones/labels
        self._bind_scroll_recursive(self.tree_scrollable_frame)
        
    def _build_tree_level(self, path, level):
        try:
            items = os.listdir(path)
            # Ordenar por fecha de modificación descendente (más recientes primero)
            items = sorted(items, key=lambda x: os.path.getmtime(os.path.join(path, x)), reverse=True)
        except PermissionError:
            return

        for item in items:
            full_path = os.path.join(path, item)
            if os.path.isdir(full_path):
                if self._has_images(full_path):
                    f_icon = assets.get_icon("folder", size=(16, 16))
                    lbl = ctk.CTkButton(
                        self.tree_scrollable_frame, 
                        text=" " + item, 
                        image=f_icon,
                        font=("Arial", 12, "bold"),
                        fg_color="transparent", 
                        text_color=("gray10", "gray90"),
                        hover=False,
                        anchor="w",
                        height=24
                    )
                    lbl.pack(fill="x", padx=(level * 15 + 5, 5), pady=(2, 0))
                    self._build_tree_level(full_path, level + 1)
            elif item.lower().endswith(('.png', '.jpg', '.jpeg')):
                f_icon = assets.get_icon("image_file", size=(16, 16))
                btn = ctk.CTkButton(
                    self.tree_scrollable_frame, 
                    text=" " + item, 
                    image=f_icon,
                    fg_color="transparent", 
                    text_color=("gray10", "gray90"),
                    hover_color=constants.TOOLTIP_BG_COLOR,
                    anchor="w",
                    height=24,
                    command=lambda p=full_path: self.on_image_selected(p)
                )
                btn.pack(fill="x", padx=(level * 15 + 15, 5), pady=1)
                self.file_buttons[full_path] = btn
                # Vincular scroll explícitamente a cada botón
                btn.bind("<MouseWheel>", self._on_mousewheel)

    def highlight_path(self, path):
        """Resalta visualmente el archivo seleccionado en el árbol de forma eficiente."""
        # 1. Resetear el botón anteriormente seleccionado si existe
        if self.active_button:
            self.active_button.configure(
                fg_color="transparent", 
                hover_color=constants.TOOLTIP_BG_COLOR,
                font=("Arial", 12, "normal")
            )
        
        # 2. Buscar y resaltar el nuevo botón
        btn = self.file_buttons.get(path)
        if btn:
            self.selected_path = path
            self.active_button = btn
            # Forzamos que el color de hover sea el mismo que el de fondo para que no "parpadee" al pasar el mouse
            btn.configure(
                fg_color="#3a3a3a", 
                hover_color="#3a3a3a", 
                font=("Arial", 12, "bold")
            )
        else:
            self.selected_path = None
            self.active_button = None

    def _has_images(self, path):
        for root, dirs, files in os.walk(path):
            for file in files:
                if file.lower().endswith(('.png', '.jpg', '.jpeg')):
                    return True
        return False
