import tkinter as tk
import os
import subprocess
from module_editor import constants

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
                         background=constants.TOOLTIP_BG_COLOR, foreground=constants.TOOLTIP_FG_COLOR, 
                         relief='solid', borderwidth=1, font=("Arial", 10))
        label.pack(ipadx=6, ipady=3)

    def hide_tooltip(self, event):
        if self.tooltip_window:
            self.tooltip_window.destroy()
            self.tooltip_window = None

def copy_image_to_clipboard(pil_image):
    """Funde la imagen y la envía al portapapeles de Windows vía PowerShell."""
    if not pil_image:
        return False
        
    tmp_path = os.path.join(os.environ.get("TEMP", "C:/Windows/Temp"), "qas_clipboard.bmp")
    
    try:
        # Convertir imagen a BMP (Windows nativo para portapapeles)
        pil_image.convert("RGB").save(tmp_path, "BMP")
        
        # Comando de PowerShell: Carga la imagen desde el archivo temporal y la pone en el clipboard
        ps_script = "[void][Reflection.Assembly]::LoadWithPartialName('System.Windows.Forms');"
        ps_script += "[void][Reflection.Assembly]::LoadWithPartialName('System.Drawing');"
        ps_script += f"$img = [System.Drawing.Image]::FromFile('{tmp_path.replace('\\', '/')}'); "
        ps_script += "[System.Windows.Forms.Clipboard]::SetImage($img); "
        ps_script += "$img.Dispose();"
        
        subprocess.run(["powershell", "-NoProfile", "-Command", ps_script], check=True)
        
        # Limpieza
        if os.path.exists(tmp_path):
            os.remove(tmp_path)
        return True
    except Exception as e:
        if os.path.exists(tmp_path):
            os.remove(tmp_path)
        print(f"Error al copiar al clipboard: {e}")
        return False
