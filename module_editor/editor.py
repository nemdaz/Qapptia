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
from module_editor.utils import Tooltip
from module_editor.editor_sidebar import EditorSidebar

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
        Tooltip(self.btn_rotate, "Rotar")
        
        icon_save = assets.get_icon("save", size=constants.ICON_SIZE)
        self.btn_save = ctk.CTkButton(self.frame_toolbar, text="", image=icon_save, width=40, command=self.save_rotation, state="disabled", fg_color="green", hover_color="darkgreen")
        self.btn_save.pack(side="left", padx=5, pady=10)
        Tooltip(self.btn_save, "Guardar")
        
        self.lbl_status = ctk.CTkLabel(self.frame_toolbar, text="Listo")
        self.lbl_status.pack(side="right", padx=10, pady=10)
        
        # --- Área Principal (Imagen) ---
        self.frame_image = ctk.CTkFrame(self)
        self.frame_image.grid(row=1, column=0, sticky="nsew", padx=(10, 5), pady=10)
        
        self.lbl_image = ctk.CTkLabel(self.frame_image, text="Por favor seleccione una imagen\no tome su primera captura.")
        self.lbl_image.pack(expand=True, fill="both", padx=10, pady=10)
        
        # Evento de Resize para estirar la imagen dinámicamente
        self.frame_image.bind("<Configure>", self.on_resize)
        
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
            self.lbl_image.configure(text="La ruta de guardado configurada no existe.")
            return
            
        # Buscar el archivo .png/.jpg más reciente
        list_of_files = []
        for root, dirs, files in os.walk(base_path):
            for file in files:
                if file.lower().endswith(".png") or file.lower().endswith(".jpg"):
                    list_of_files.append(os.path.join(root, file))
                    
        if not list_of_files:
            self.lbl_image.configure(text="No hay capturas en el directorio configurado.")
            return
            
        latest_file = max(list_of_files, key=os.path.getmtime)
        self.show_image(latest_file)
        
    def show_image(self, path):
        try:
            self.current_image_path = path
            self.current_pil_image = Image.open(path)
            self.current_rotation = 0
            
            self._render_current_image()
            
            self.btn_rotate.configure(state="normal")
            self.lbl_status.configure(text=f"Mostrando: {os.path.basename(path)}")
            # Reset save button
            self.btn_save.configure(state="disabled")
        except Exception as e:
            self.lbl_status.configure(text=f"Error cargando imagen: {e}")
            
    def on_resize(self, event):
        # Usar las dimensiones reales del frame actualizadas por Windows, no los del evento hijo de tkinter
        current_w = self.frame_image.winfo_width()
        current_h = self.frame_image.winfo_height()
        
        # Prevenir re-renderizados si las dimensiones no han cambiado
        if hasattr(self, '_last_w') and self._last_w == current_w and self._last_h == current_h:
            return
            
        self._last_w = current_w
        self._last_h = current_h
        
        # Debouncing ultra rápido. Suficiente para procesar los batch events de un Maximizar 
        # sin que se borre la imagen
        if self._resize_timeout:
            self.after_cancel(self._resize_timeout)
        self._resize_timeout = self.after(constants.DEBOUNCE_DELAY_MS, self._render_current_image)
            
    def _render_current_image(self):
        if not self.current_pil_image: return
        
        self.update_idletasks() # Asegurar dimensiones actualizadas
        
        img_rotated = self.current_pil_image.rotate(-self.current_rotation, expand=True) # Anti-horario
        
        # Escalar para que encaje en el frame (con margen)
        frame_w = self.frame_image.winfo_width()
        frame_h = self.frame_image.winfo_height()
        
        if frame_w <= 1 or frame_h <= 1:
            frame_w, frame_h = 700, 500
            
        img_w, img_h = img_rotated.size
        ratio = min((frame_w - 20) / img_w, (frame_h - 20) / img_h)
        
        if ratio > 1: ratio = 1 # Prevenir escalado excesivo de imágenes pequeñas
        
        new_size = (int(img_w * ratio), int(img_h * ratio))
        
        ctk_img = ctk.CTkImage(light_image=img_rotated, dark_image=img_rotated, size=new_size)
        self.lbl_image.configure(image=ctk_img, text="") 
        self.lbl_image._image = ctk_img 
        
    def resize_sidebar(self, event):
        app_right_edge = self.winfo_rootx() + self.winfo_width()
        new_width = app_right_edge - event.x_root - 10 # 10 = Padding
        
        if new_width < 150: new_width = 150
        if new_width > 600: new_width = 600
        
        self.grid_columnconfigure(2, minsize=new_width)
        
    def rotate_image(self):
        if self.current_pil_image:
            self.current_rotation = (self.current_rotation + 90) % 360 
            self._render_current_image()
            self.btn_save.configure(state="normal") # Habilitar botón de guardado

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
                self.lbl_status.configure(text=f"¡Rotación guardada en el archivo original!")
            except Exception as e:
                self.lbl_status.configure(text=f"Error al guardar: {e}")

def run_editor():
    app = EditorApp()
    app.mainloop()

if __name__ == "__main__":
    run_editor()
