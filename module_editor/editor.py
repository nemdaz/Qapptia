import customtkinter as ctk
import sys
import os
from PIL import Image

# Configurar ruta base del proyecto
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from core import config, assets
from module_editor import constants, state_manager, utils
from module_editor.editor_sidebar import EditorSidebar
from module_editor.editor_canvas import VectorCanvas

class EditorApp(ctk.CTk):
    """Aplicación principal del Editor de Capturas."""
    
    def __init__(self):
        super().__init__()
        
        self.title(constants.WINDOW_TITLE)
        self.geometry(constants.WINDOW_SIZE)
        self.minsize(constants.MIN_WIDTH, constants.MIN_HEIGHT)
        
        # Variables de estado
        self.current_image_path = None
        self.current_pil_image = None
        self.current_rotation = 0
        self.active_tool_btn = None 
        
        self.setup_ui()
        
        # Carga inicial diferida para asegurar renderizado de UI
        self.after(constants.INITIAL_LOAD_DELAY_MS, self.load_latest_image)
        self.bind("<Button-1>", self.on_window_click)

    def on_window_click(self, event):
        """Deselecciona herramientas si se clica fuera de la zona de dibujo."""
        if not self.active_tool_btn: return
        try:
            widget = self.winfo_containing(event.x_root, event.y_root)
            if not widget: return
            w_str = str(widget)
            if w_str.startswith(str(self.active_tool_btn)) or w_str.startswith(str(self.vector_canvas)):
                return
        except: pass
        self.set_tool(None)

    def setup_ui(self):
        self.grid_rowconfigure(1, weight=1)
        self.grid_columnconfigure(0, weight=1) 
        self.grid_columnconfigure(1, weight=0, minsize=5) 
        self.grid_columnconfigure(2, weight=0, minsize=constants.SIDEBAR_WIDTH)
        
        # --- Toolbar ---
        self.toolbar = ctk.CTkFrame(self, height=50)
        self.toolbar.grid(row=0, column=0, columnspan=3, sticky="ew", padx=10, pady=(10, 0))
        
        # Botones de Acción
        self.btn_rotate = self._create_toolbar_btn("rotate", self.rotate_image, "Rotar")
        self.btn_copy_file = self._create_toolbar_btn("copy_file", None, "Copiar Archivo")
        self.btn_copy_clip = self._create_toolbar_btn("copy_clip", self.copy_to_clipboard_with_deselect, "Copiar al Portapapeles")
        
        self.btn_save = self._create_toolbar_btn("save", self.save_rotation, "Guardar", fg="green", hover="darkgreen")
        self.btn_save.configure(state="disabled")
        
        # Separador
        ctk.CTkFrame(self.toolbar, width=2, height=30, fg_color="gray30").pack(side="left", padx=10, pady=10)
        
        # Botones de Herramientas
        self.btn_arrow = self._create_toolbar_btn("arrow", lambda: self.set_tool("arrow"), "Dibujar Flecha")
        self.btn_rect = self._create_toolbar_btn("rect", lambda: self.set_tool("rect"), "Dibujar Rectángulo")
        
        # --- Canvas y Scrollbars ---
        self.frame_image = ctk.CTkFrame(self)
        self.frame_image.grid(row=1, column=0, sticky="nsew", padx=(10, 5), pady=10)
        self.frame_image.grid_rowconfigure(0, weight=1)
        self.frame_image.grid_columnconfigure(0, weight=1)
        
        self.vector_canvas = VectorCanvas(self.frame_image, on_zoom_callback=self.update_scrollbar_visibility)
        self.vector_canvas.grid(row=0, column=0, sticky="nsew")
        
        self.v_scrollbar = ctk.CTkScrollbar(self.frame_image, orientation="vertical", command=self.vector_canvas.yview)
        self.h_scrollbar = ctk.CTkScrollbar(self.frame_image, orientation="horizontal", command=self.vector_canvas.xview)
        self.vector_canvas.configure(yscrollcommand=self.v_scrollbar.set, xscrollcommand=self.h_scrollbar.set)
        
        # --- Drag Handle (Sidebar Resize) ---
        self.drag_handle = ctk.CTkFrame(self, width=5, cursor="sb_h_double_arrow", fg_color="transparent")
        self.drag_handle.grid(row=1, column=1, sticky="ns", pady=10)
        self.drag_handle.bind("<B1-Motion>", self.resize_sidebar)
        self.drag_handle.bind("<Enter>", lambda e: self.drag_handle.configure(fg_color="gray50"))
        self.drag_handle.bind("<Leave>", lambda e: self.drag_handle.configure(fg_color="transparent"))
        
        # --- Sidebar ---
        self.sidebar = EditorSidebar(self, on_image_selected_callback=self.show_image)
        self.sidebar.grid(row=1, column=2, sticky="nsew", padx=(0, 10), pady=10)

    def _create_toolbar_btn(self, icon_name, command, tooltip, fg=None, hover=None):
        btn = ctk.CTkButton(
            self.toolbar, text="", image=assets.get_icon(icon_name), 
            width=40, command=command, fg_color=fg, hover_color=hover
        )
        btn.pack(side="left", padx=5, pady=10)
        utils.Tooltip(btn, tooltip)
        return btn

    def load_latest_image(self):
        """Carga la imagen desde el histórico o busca la más reciente en disco."""
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
        """Activa/Desactiva herramientas de dibujo con feedback visual."""
        if self.active_tool_btn:
            self.active_tool_btn.configure(fg_color=ctk.ThemeManager.theme["CTkButton"]["fg_color"])
            
        btn = self.btn_arrow if tool == "arrow" else (self.btn_rect if tool == "rect" else None)
            
        if btn and self.active_tool_btn != btn:
            self.active_tool_btn = btn
            btn.configure(fg_color=constants.ACTIVE_TOOL_COLOR)
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
