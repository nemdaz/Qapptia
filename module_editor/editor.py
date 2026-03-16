import customtkinter as ctk
import sys
import os
import tkinter as tk
from PIL import Image

# Configurar ruta base del proyecto (para alcanzar module_capture, core, etc)
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from core import config
from core import assets
from module_editor import constants
from module_editor.editor_sidebar import EditorSidebar
from module_editor.editor_canvas import VectorCanvas
from module_editor import utils

ctk.set_appearance_mode("Dark")
ctk.set_default_color_theme("blue")

class EditorApp(ctk.CTk):
    def __init__(self):
        super().__init__()
        
        self.title(constants.WINDOW_TITLE)
        self.geometry(constants.WINDOW_SIZE)
        self.minsize(constants.MIN_WIDTH, constants.MIN_HEIGHT)
        
        # Variables de estado
        self.current_image_path = None
        self.current_pil_image = None
        self.current_rotation = 0
        self._resize_timeout = None
        
        self.setup_ui()
        
        # Cargar imagen inicial después de renderizar el layout
        self.after(constants.INITIAL_LOAD_DELAY_MS, self.load_latest_image)

    def setup_ui(self):
        # Configurar grid principal (2 filas, 3 columnas)
        self.grid_rowconfigure(1, weight=1)
        self.grid_columnconfigure(0, weight=1) # Área de imagen cede espacio
        self.grid_columnconfigure(1, weight=0, minsize=5) # Separador Drag
        self.grid_columnconfigure(2, weight=0, minsize=constants.SIDEBAR_WIDTH) # Panel lateral de ancho fijo
        
        # --- Toolbar Superior ---
        self.frame_toolbar = ctk.CTkFrame(self, height=50)
        self.frame_toolbar.grid(row=0, column=0, columnspan=3, sticky="ew", padx=10, pady=(10, 0))
        
        icon_rotate = assets.get_icon("rotate", size=constants.ICON_SIZE)
        self.btn_rotate = ctk.CTkButton(self.frame_toolbar, text="", image=icon_rotate, width=40, command=self.rotate_image, state="disabled")
        self.btn_rotate.pack(side="left", padx=10, pady=10)
        utils.Tooltip(self.btn_rotate, "Rotar")
        
        icon_copy_file = assets.get_icon("copy_file", size=constants.ICON_SIZE)
        self.btn_copy_file = ctk.CTkButton(self.frame_toolbar, text="", image=icon_copy_file, width=40, state="disabled")
        self.btn_copy_file.pack(side="left", padx=5, pady=10)
        utils.Tooltip(self.btn_copy_file, "Copiar Archivo")
        
        icon_copy_clip = assets.get_icon("copy_clip", size=constants.ICON_SIZE)
        self.btn_copy_clip = ctk.CTkButton(self.frame_toolbar, text="", image=icon_copy_clip, width=40, state="disabled", command=self.copy_to_clipboard)
        self.btn_copy_clip.pack(side="left", padx=5, pady=10)
        utils.Tooltip(self.btn_copy_clip, "Copiar al Portapapeles")
        
        icon_save = assets.get_icon("save", size=constants.ICON_SIZE)
        self.btn_save = ctk.CTkButton(self.frame_toolbar, text="", image=icon_save, width=40, command=self.save_rotation, state="disabled", fg_color="green", hover_color="darkgreen")
        self.btn_save.pack(side="left", padx=5, pady=10)
        utils.Tooltip(self.btn_save, "Guardar")
        
        # Divisor
        ctk.CTkFrame(self.frame_toolbar, width=2, height=30, fg_color="gray30").pack(side="left", padx=10, pady=10)
        
        icon_arrow = assets.get_icon("arrow", size=constants.ICON_SIZE)
        self.btn_arrow = ctk.CTkButton(self.frame_toolbar, text="", image=icon_arrow, width=40, command=lambda: self.vector_canvas.set_draw_mode("arrow"))
        self.btn_arrow.pack(side="left", padx=5, pady=10)
        utils.Tooltip(self.btn_arrow, "Dibujar Flecha")
        
        icon_rect = assets.get_icon("rect", size=constants.ICON_SIZE)
        self.btn_rect = ctk.CTkButton(self.frame_toolbar, text="", image=icon_rect, width=40, command=lambda: self.vector_canvas.set_draw_mode("rect"))
        self.btn_rect.pack(side="left", padx=5, pady=10)
        utils.Tooltip(self.btn_rect, "Dibujar Rectángulo")
        
        # --- Área de imagen (ahoras con Scrollbars) ---
        self.frame_image = ctk.CTkFrame(self)
        self.frame_image.grid(row=1, column=0, sticky="nsew", padx=(10, 5), pady=10)
        
        # Grid para el canvas y scrollbars dentro del frame
        self.frame_image.grid_rowconfigure(0, weight=1)
        self.frame_image.grid_columnconfigure(0, weight=1)
        
        self.vector_canvas = VectorCanvas(self.frame_image, on_zoom_callback=self.update_scrollbar_visibility)
        self.vector_canvas.grid(row=0, column=0, sticky="nsew")
        
        # Scrollbars
        self.v_scrollbar = ctk.CTkScrollbar(self.frame_image, orientation="vertical", command=self.vector_canvas.yview)
        self.h_scrollbar = ctk.CTkScrollbar(self.frame_image, orientation="horizontal", command=self.vector_canvas.xview)
        
        self.vector_canvas.configure(yscrollcommand=self.v_scrollbar.set, xscrollcommand=self.h_scrollbar.set)
        
        # Iniciar vinculación de visibilidad de scrollbars
        self.vector_canvas.bind("<Configure>", lambda e: self.update_scrollbar_visibility(), add="+")
        
        # --- Drag Handle ---
        self.drag_handle = ctk.CTkFrame(self, width=5, cursor="sb_h_double_arrow", fg_color="transparent")
        self.drag_handle.grid(row=1, column=1, sticky="ns", pady=10)
        self.drag_handle.bind("<B1-Motion>", self.resize_sidebar)
        self.drag_handle.bind("<Enter>", lambda e: self.drag_handle.configure(fg_color="gray50"))
        self.drag_handle.bind("<Leave>", lambda e: self.drag_handle.configure(fg_color="transparent"))
        
        # --- Panel Lateral (Árbol de Archivos) ---
        self.sidebar = EditorSidebar(self, on_image_selected_callback=self.show_image)
        self.sidebar.grid(row=1, column=2, sticky="nsew", padx=(0, 10), pady=10)
        
    def load_latest_image(self):
        base_path = os.path.expandvars(config.get("save_path"))
        if not os.path.exists(base_path):
            self.lbl_status.configure(text="La ruta de guardado configurada no existe.")
            return
            
        # Buscar el archivo .png/.jpg más reciente
        list_of_files = []
        for root, dirs, files in os.walk(base_path):
            for file in files:
                if file.lower().endswith(".png") or file.lower().endswith(".jpg"):
                    list_of_files.append(os.path.join(root, file))
                    
        if not list_of_files:
            self.lbl_status.configure(text="No hay capturas en el directorio configurado.")
            return
            
        latest_file = max(list_of_files, key=os.path.getmtime)
        self.show_image(latest_file)
        
    def show_image(self, img_path):
        if not os.path.exists(img_path): return
        
        # Optimización: No recargar si es la misma imagen
        if self.current_image_path == img_path:
            return
            
        try:
            self.current_image_path = img_path
            self.current_pil_image = Image.open(img_path)
            self.current_rotation = 0
            
            # Pasar imagen y metadata URL al motor abstracto VectorCanvas
            self.vector_canvas.load_image(self.current_pil_image, img_path)
            
            filename = os.path.basename(img_path)
            self.title(f"{constants.WINDOW_TITLE} - {filename}")
            
            self.btn_rotate.configure(state="normal")
            self.btn_copy_file.configure(state="normal")
            self.btn_copy_clip.configure(state="normal")
            self.btn_save.configure(state="normal")
            
            # Resaltar en Sidebar y limpiar status de la toolbar
            self.sidebar.highlight_path(img_path)
        except Exception as e:
            utils.show_toast(self, "Error al abrir la imagen")
            print(f"Error show_image {img_path}: {e}")
            
    def resize_sidebar(self, event):
        app_right_edge = self.winfo_rootx() + self.winfo_width()
        new_width = app_right_edge - event.x_root - 10 # 10 = Padding
        
        if new_width < 150: new_width = 150
        if new_width > 600: new_width = 600
        
        self.grid_columnconfigure(2, minsize=new_width)

    def update_scrollbar_visibility(self):
        """Muestra u oculta los scrollbars dependiendo de si la imagen cabe en el canvas."""
        # Forzar actualización de geometría para tener datos frescos
        self.update_idletasks()
        
        sr = self.vector_canvas.cget("scrollregion")
        if not sr: return
        
        # sr es una cadena "0 0 width height" o similar
        _, _, sr_w, sr_h = map(float, sr.split())
        
        canvas_w = self.vector_canvas.winfo_width()
        canvas_h = self.vector_canvas.winfo_height()
        
        # Vertical
        if sr_h > canvas_h + 1:
            self.v_scrollbar.grid(row=0, column=1, sticky="ns")
        else:
            self.v_scrollbar.grid_forget()
            
        # Horizontal
        if sr_w > canvas_w + 1:
            self.h_scrollbar.grid(row=1, column=0, sticky="ew")
        else:
            self.h_scrollbar.grid_forget()
        
    def rotate_image(self):
        if self.current_pil_image:
            self.current_rotation = (self.current_rotation + 90) % 360 
            # Calcular en base a self.current_rotation en lugar de un ángulo fijo
            img_rotated = self.current_pil_image.rotate(-self.current_rotation, expand=True)
            self.vector_canvas.load_image(img_rotated, self.current_image_path)
            utils.show_toast(self, f"Rotado {self.current_rotation}º")

    def copy_to_clipboard(self):
        if self.vector_canvas.copy_to_clipboard():
            utils.show_toast(self, "¡Imagen copiada al portapapeles!")
        else:
            utils.show_toast(self, "Error al copiar imagen")
            
    def save_rotation(self):
        if self.current_pil_image and self.current_image_path and self.current_rotation != 0:
            try:
                # Guardar en disco la imagen rotada
                img_rotated = self.current_pil_image.rotate(-self.current_rotation, expand=True)
                img_rotated.save(self.current_image_path)
                
                # Actualizar estado con la nueva imagen
                self.current_pil_image = Image.open(self.current_image_path)
                self.current_rotation = 0
                self.btn_save.configure(state="disabled")
                utils.show_toast(self, "¡Imagen guardada!")
            except Exception as e:
                utils.show_toast(self, f"Error al guardar")
                print(f"Error al guardar: {e}")

def run_editor():
    app = EditorApp()
    app.mainloop()

if __name__ == "__main__":
    run_editor()
