import customtkinter as ctk
import tkinter as tk
import sys
import os
from PIL import Image
import glob

# Configurar ruta base del proyecto
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from core import config
from core import assets

class Tooltip:
    def __init__(self, widget, text):
        self.widget = widget
        self.text = text
        self.tooltip_window = None
        self.widget.bind("<Enter>", self.show_tooltip)
        self.widget.bind("<Leave>", self.hide_tooltip)

    def show_tooltip(self, event):
        if self.tooltip_window is not None:
            return
        x = self.widget.winfo_rootx() + (self.widget.winfo_width() // 2) - 15
        y = self.widget.winfo_rooty() + self.widget.winfo_height() + 5
        
        self.tooltip_window = tk.Toplevel(self.widget)
        self.tooltip_window.wm_overrideredirect(True)
        self.tooltip_window.wm_geometry(f"+{x}+{y}")
        self.tooltip_window.attributes("-topmost", True)
        
        label = tk.Label(self.tooltip_window, text=self.text, justify='left',
                         background="#2b2b2b", foreground="white", relief='solid', borderwidth=1,
                         font=("Arial", 10))
        label.pack(ipadx=6, ipady=3)

    def hide_tooltip(self, event):
        if self.tooltip_window:
            self.tooltip_window.destroy()
            self.tooltip_window = None

ctk.set_appearance_mode("Dark")
ctk.set_default_color_theme("blue")

class EditorApp(ctk.CTk):
    def __init__(self):
        super().__init__()
        
        self.title("Editor de Capturas")
        self.geometry("1000x600")
        self.minsize(800, 500)
        
        # Variables de estado
        self.current_image_path = None
        self.current_pil_image = None
        self.current_rotation = 0
        self._resize_timeout = None
        
        self.setup_ui()
        
        # Cargar imagen inicial después de renderizar el layout
        self.after(200, self.load_latest_image)

    def setup_ui(self):
        # Configurar grid principal (2 filas, 2 columnas)
        self.grid_rowconfigure(1, weight=1)
        self.grid_columnconfigure(0, weight=3) # Área de imagen toma más espacio
        self.grid_columnconfigure(1, weight=1) # Panel lateral
        
        # --- Toolbar Superior ---
        self.frame_toolbar = ctk.CTkFrame(self, height=50)
        self.frame_toolbar.grid(row=0, column=0, columnspan=2, sticky="ew", padx=10, pady=(10, 0))
        
        icon_rotate = assets.get_icon("rotate", size=(20, 20))
        self.btn_rotate = ctk.CTkButton(self.frame_toolbar, text="", image=icon_rotate, width=40, command=self.rotate_image, state="disabled")
        self.btn_rotate.pack(side="left", padx=10, pady=10)
        Tooltip(self.btn_rotate, "Rotar")
        
        icon_save = assets.get_icon("save", size=(20, 20))
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
        
        # --- Panel Lateral (Árbol de Archivos) ---
        self.frame_tree = ctk.CTkFrame(self)
        self.frame_tree.grid(row=1, column=1, sticky="nsew", padx=(5, 10), pady=10)
        
        self.lbl_tree_title = ctk.CTkLabel(self.frame_tree, text="Explorador", font=("Arial", 14, "bold"))
        self.lbl_tree_title.pack(pady=10, padx=10, anchor="w")
        
        # Placeholder para el explorador (a construirse de forma dinámica después)
        self.tree_scrollable_frame = ctk.CTkScrollableFrame(self.frame_tree)
        self.tree_scrollable_frame.pack(expand=True, fill="both", padx=10, pady=(0, 10))
        
        self.populate_tree_placeholder()

    def populate_tree_placeholder(self):
        base_path = os.path.expandvars(config.get("save_path"))
        ctk.CTkLabel(self.tree_scrollable_frame, text=f"Ruta Base:\n{base_path}", text_color="gray", justify="left").pack(anchor="w", pady=5)
        
        btn = ctk.CTkButton(self.tree_scrollable_frame, text="Ver última captura", command=self.load_latest_image)
        btn.pack(pady=10)
        
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
        
        # Debouncing ultra rápido. Suffciente para procesar los batch events de un Maximizar 
        # sin que se borre la imagen
        if self._resize_timeout:
            self.after_cancel(self._resize_timeout)
        self._resize_timeout = self.after(20, self._render_current_image)
            
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
