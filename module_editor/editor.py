import customtkinter as ctk
import sys
import os
from PIL import Image

# Configurar ruta base del proyecto
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from core import config, assets, ipc
from module_editor import constants, state_manager, utils
from module_editor.editor_sidebar import EditorSidebar
from module_editor.editor_canvas import VectorCanvas

class EditorApp(ctk.CTk):
    """Aplicación principal del Editor de Capturas."""
    
    def __init__(self):
        super().__init__()
        
        # Servidor IPC
        ipc.start_ipc_server(self.wake_up)
        
        self.title(constants.WINDOW_TITLE)
        self.geometry(constants.WINDOW_SIZE)
        self.minsize(constants.MIN_WIDTH, constants.MIN_HEIGHT)
        
        # Variables de estado
        self.current_image_path = None
        self.current_pil_image = None
        self.current_rotation = 0
        self.active_tool_btn = None 
        self.active_color_btn = None
        self.color_btns = {}
        
        # Cargar estado
        self.editor_state = state_manager.load_state()
        self.current_color_name = self.editor_state.get("active_fav_color", constants.DEFAULT_FAV_COLOR)
        self.current_color_hex = constants.FAVORITE_COLORS.get(self.current_color_name, "#00ff00")
        
        self.setup_ui()
        
        self.after(constants.INITIAL_LOAD_DELAY_MS, self.load_latest_image)
        self.bind("<Button-1>", self.on_window_click)

    def wake_up(self):
        """Trae la ventana al frente y refresca el contenido."""
        self.after(0, self._handle_wake_up)

    def _handle_wake_up(self):
        """Intenta traer la ventana al frente con seguridad."""
        try:
            self.deiconify()
            # Forzar ventana al frente
            self.attributes("-topmost", True)
            self.attributes("-topmost", False)
            self.lift()
            self.focus_force()
        except Exception as e:
            print(f"Error al intentar enfocar el editor: {e}")
            # Fallback: desminimizar
            try: self.deiconify()
            except: pass
            
        # El refresco de la sidebar es independiente del foco de la ventana
        try:
            self.sidebar.refresh_all()
        except Exception as e:
            print(f"Error al refrescar sidebar en wake_up: {e}")

    def on_window_click(self, event):
        """Deselecciona herramientas si se clica fuera de la zona de dibujo o toolbar."""
        if not self.active_tool_btn: return
        try:
            widget = self.winfo_containing(event.x_root, event.y_root)
            if not widget: return
            w_str = str(widget)
            # Evitar deselección en herramientas o canvas
            if (w_str.startswith(str(self.active_tool_btn)) or 
                w_str.startswith(str(self.vector_canvas)) or
                w_str.startswith(str(self.toolbar))):
                return
        except: pass
        self.set_tool(None)

    def setup_ui(self):
        self.grid_rowconfigure(1, weight=1)
        self.grid_columnconfigure(0, weight=1) 
        self.grid_columnconfigure(1, weight=0, minsize=5) 
        self.grid_columnconfigure(2, weight=0, minsize=constants.SIDEBAR_WIDTH)
        
        # Toolbar
        self.toolbar = ctk.CTkFrame(self, height=50)
        self.toolbar.grid(row=0, column=0, columnspan=3, sticky="ew", padx=10, pady=(10, 0))
        
        # Acciones
        self.btn_save = self._create_toolbar_btn("save", self.save_rotation, constants.TOOLTIPS["save"])
        self.btn_save.configure(state="disabled")
        
        self.btn_rotate = self._create_toolbar_btn("rotate", self.rotate_image, constants.TOOLTIPS["rotate"])
        self.btn_copy_file = self._create_toolbar_btn("copy_file", self.copy_file_to_clipboard, constants.TOOLTIPS["copy_file"])
        self.btn_copy_clip = self._create_toolbar_btn("copy_clip", self.copy_to_clipboard_with_deselect, constants.TOOLTIPS["copy_clip"])
        
        # Separador
        ctk.CTkFrame(self.toolbar, width=2, height=30, fg_color="gray30").pack(side="left", padx=10, pady=10)
        
        # Herramientas de dibujo
        self.btn_arrow = self._create_toolbar_btn("arrow", lambda: self.set_tool("arrow"), constants.TOOLTIPS["arrow"])
        self.btn_rect = self._create_toolbar_btn("rect", lambda: self.set_tool("rect"), constants.TOOLTIPS["rect"])
        self.btn_highlighter = self._create_toolbar_btn("highlighter", lambda: self.set_tool("highlighter"), constants.TOOLTIPS["highlighter"])
        
        # Separador
        ctk.CTkFrame(self.toolbar, width=2, height=30, fg_color="gray30").pack(side="left", padx=10, pady=10)
        
        # Colores favoritos
        self._create_favorites_palette()
        
        # Canvas y scrollbars
        self.frame_image = ctk.CTkFrame(self)
        self.frame_image.grid(row=1, column=0, sticky="nsew", padx=(10, 5), pady=10)
        self.frame_image.grid_rowconfigure(0, weight=1)
        self.frame_image.grid_columnconfigure(0, weight=1)
        
        self.vector_canvas = VectorCanvas(self.frame_image, on_zoom_callback=self.update_scrollbar_visibility)
        self.vector_canvas.grid(row=0, column=0, sticky="nsew")
        
        self.v_scrollbar = ctk.CTkScrollbar(self.frame_image, orientation="vertical", command=self.vector_canvas.yview)
        self.h_scrollbar = ctk.CTkScrollbar(self.frame_image, orientation="horizontal", command=self.vector_canvas.xview)
        self.vector_canvas.configure(yscrollcommand=self.v_scrollbar.set, xscrollcommand=self.h_scrollbar.set)
        
        # Redimensionado lateral
        self.drag_handle = ctk.CTkFrame(self, width=5, cursor="sb_h_double_arrow", fg_color="transparent")
        self.drag_handle.grid(row=1, column=1, sticky="ns", pady=10)
        self.drag_handle.bind("<B1-Motion>", self.resize_sidebar)
        self.drag_handle.bind("<Enter>", lambda e: self.drag_handle.configure(fg_color="gray50"))
        self.drag_handle.bind("<Leave>", lambda e: self.drag_handle.configure(fg_color="transparent"))
        
        # Sidebar
        self.sidebar = EditorSidebar(self, on_image_selected_callback=self.show_image)
        self.sidebar.grid(row=1, column=2, sticky="nsew", padx=(0, 10), pady=10)

    def _create_toolbar_btn(self, icon_name, command, tooltip, fg="transparent", hover=None):
        hover = hover or constants.HIGHLIGHT_COLOR
        btn = ctk.CTkButton(
            self.toolbar, text="", image=assets.get_icon(icon_name), 
            width=40, command=command, fg_color=fg, hover_color=hover
        )
        btn.pack(side="left", padx=5, pady=10)
        utils.Tooltip(btn, tooltip)
        return btn

    def _create_favorites_palette(self):
        """Fila de colores favoritos."""
        for name, hex_val in constants.FAVORITE_COLORS.items():
            icon_img = assets.create_color_square_icon(hex_val)
            ctk_icon = ctk.CTkImage(light_image=icon_img, dark_image=icon_img, size=(20, 20))
            
            btn = ctk.CTkButton(
                self.toolbar, text="", image=ctk_icon, width=32, height=32,
                fg_color="transparent", hover_color=constants.HIGHLIGHT_COLOR,
                command=lambda n=name, h=hex_val: self.set_active_color(n, h)
            )
            btn.pack(side="left", padx=2, pady=10)
            self.color_btns[name] = btn
            
            display_name = constants.FAVORITE_COLOR_NAMES.get(name, name.capitalize())
            utils.Tooltip(btn, f"{constants.TOOLTIPS['color_prefix']}{display_name}")
        
        self.update_color_ui()

    def set_active_color(self, name, hex_val):
        """Cambia el color activo para los nuevos vectores y actualiza el seleccionado."""
        self.current_color_name = name
        self.current_color_hex = hex_val
        state_manager.set_active_color(name)
        self.update_color_ui()
        self.vector_canvas.change_selected_color(hex_val)

    def update_color_ui(self):
        """UI de selección de color."""
        for name, btn in self.color_btns.items():
            if name == self.current_color_name:
                btn.configure(border_width=2, border_color=constants.ACTIVE_TOOL_COLOR)
                self.active_color_btn = btn
            else:
                btn.configure(border_width=0)

    def load_latest_image(self):
        """Cargar última imagen."""
        state = state_manager.load_state()
        last_file = state.get("last_selected_file")
        
        if last_file and os.path.exists(last_file):
            self.show_image(last_file)
            return

        base_path = os.path.expandvars(config.get("save_path"))
        if not os.path.exists(base_path): return
            
        files = []
        for root, _, fnames in os.walk(base_path):
            for f in fnames:
                if f.lower().endswith((".png", ".jpg", ".jpeg")):
                    files.append(os.path.join(root, f))
                    
        if files:
            self.show_image(max(files, key=os.path.getmtime))

    def show_image(self, path):
        if not os.path.exists(path) or self.current_image_path == path: return
        self.set_tool(None)
        try:
            self.current_image_path = path
            self.current_pil_image = Image.open(path)
            self.current_rotation = 0
            
            self.vector_canvas.load_image(self.current_pil_image, path)
            self.title(f"{constants.WINDOW_TITLE} - {os.path.basename(path)}")
            
            for btn in [self.btn_rotate, self.btn_copy_file, self.btn_copy_clip, self.btn_save]:
                btn.configure(state="normal")
            
            self.sidebar.highlight_path(path)
        except Exception as e:
            utils.show_toast(self, "Error al abrir la imagen")
            print(f"Error show_image: {e}")

    def resize_sidebar(self, event):
        new_width = (self.winfo_rootx() + self.winfo_width()) - event.x_root - 10
        new_width = max(150, min(600, new_width))
        self.grid_columnconfigure(2, minsize=new_width)

    def set_tool(self, tool):
        """Activa/Desactiva herramientas con resaltado de borde de alto contraste."""
        if self.active_tool_btn:
            self.active_tool_btn.configure(border_width=0)
            
        btn = None
        if tool == "arrow": btn = self.btn_arrow
        elif tool == "rect": btn = self.btn_rect
        elif tool == "highlighter": btn = self.btn_highlighter
            
        if btn and self.active_tool_btn != btn:
            self.active_tool_btn = btn
            btn.configure(border_width=2, border_color=constants.ACTIVE_TOOL_COLOR)
            self.vector_canvas.set_draw_mode(tool)
        else:
            self.active_tool_btn = None
            self.vector_canvas.set_draw_mode(None)

    def update_scrollbar_visibility(self):
        self.update_idletasks()
        sr = self.vector_canvas.cget("scrollregion")
        if not sr: return
        _, _, sr_w, sr_h = map(float, sr.split())
        
        if sr_h > self.vector_canvas.winfo_height() + 1: self.v_scrollbar.grid(row=0, column=1, sticky="ns")
        else: self.v_scrollbar.grid_forget()
            
        if sr_w > self.vector_canvas.winfo_width() + 1: self.h_scrollbar.grid(row=1, column=0, sticky="ew")
        else: self.h_scrollbar.grid_forget()

    def rotate_image(self):
        if self.current_pil_image:
            self.set_tool(None)
            self.current_rotation = (self.current_rotation + 90) % 360 
            img = self.current_pil_image.rotate(-self.current_rotation, expand=True)
            self.vector_canvas.load_image(img, self.current_image_path)
            utils.show_toast(self, f"Rotado {self.current_rotation}º")

    def copy_to_clipboard_with_deselect(self):
        self.set_tool(None)
        if self.vector_canvas.copy_to_clipboard():
            utils.show_toast(self, "¡Imagen copiada!")
        else:
            utils.show_toast(self, "Error al copiar")
        
    def copy_file_to_clipboard(self):
        """Copia la ruta del archivo actual al portapapeles."""
        if self.current_image_path:
            self.clipboard_clear()
            self.clipboard_append(self.current_image_path)
            self.update() # Refrescar portapapeles
            utils.show_toast(self, "¡Ruta copiada!")

    def save_rotation(self):
        if self.current_pil_image and self.current_image_path and self.current_rotation != 0:
            self.set_tool(None)
            try:
                img = self.current_pil_image.rotate(-self.current_rotation, expand=True)
                img.save(self.current_image_path)
                self.current_pil_image = Image.open(self.current_image_path)
                self.current_rotation = 0
                self.btn_save.configure(state="disabled")
                utils.show_toast(self, "¡Imagen guardada!")
            except Exception as e:
                utils.show_toast(self, "Error al guardar")

if __name__ == "__main__":
    app = EditorApp()
    app.mainloop()
