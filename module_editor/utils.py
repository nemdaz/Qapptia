import tkinter as tk
import os
import subprocess
import customtkinter as ctk
import io
import ctypes
from ctypes import wintypes
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
    """Copia al portapapeles (Win32 API)."""
    if not pil_image:
        return False

    try:
        # Convertir a BMP (DIB)
        output = io.BytesIO()
        pil_image.convert("RGB").save(output, format="BMP")
        data = output.getvalue()[14:] # Omitir cabecera BMP
        output.close()

        # Win32 API
        CF_DIB = 8
        GMEM_MOVEABLE = 0x0002
        
        user32 = ctypes.windll.user32
        kernel32 = ctypes.windll.kernel32
        
        kernel32.GlobalAlloc.argtypes = [wintypes.UINT, ctypes.c_size_t]
        kernel32.GlobalLock.restype = wintypes.LPVOID
        kernel32.GlobalLock.argtypes = [wintypes.HGLOBAL]
        kernel32.GlobalUnlock.argtypes = [wintypes.HGLOBAL]
        user32.OpenClipboard.argtypes = [wintypes.HWND]
        user32.SetClipboardData.argtypes = [wintypes.UINT, wintypes.HANDLE]

        # Memoria global
        h_mem = kernel32.GlobalAlloc(GMEM_MOVEABLE, len(data))
        if not h_mem: return False
            
        p_mem = kernel32.GlobalLock(h_mem)
        if not p_mem: return False
            
        ctypes.memmove(p_mem, data, len(data))
        kernel32.GlobalUnlock(h_mem)
        
        # Portapapeles
        if user32.OpenClipboard(None):
            try:
                user32.EmptyClipboard()
                user32.SetClipboardData(CF_DIB, h_mem)
            finally:
                user32.CloseClipboard()
            return True
        return False
    except Exception as e:
        print(f"Error nativo al copiar al clipboard: {e}")
        return False

def show_toast(parent, message, duration=2000):
    """Notificación flotante."""
    toast = tk.Toplevel(parent)
    toast.overrideredirect(True)
    toast.attributes("-topmost", True)
    toast.attributes("-alpha", 0.0)
    
    toast.config(bg="gray10")

    frame = ctk.CTkFrame(toast, fg_color="#333333", corner_radius=10, border_width=1, border_color="gray50")
    frame.pack(padx=2, pady=2)
    
    label = ctk.CTkLabel(frame, text=message, font=("Arial", 13, "bold"), text_color="white", padx=20, pady=10)
    label.pack()

    # Posición de centrado
    px = parent.winfo_rootx() + (parent.winfo_width() // 2)
    py = parent.winfo_rooty() + (parent.winfo_height() // 2)
    
    tw = toast.winfo_width()
    th = toast.winfo_height()
    
    toast.geometry(f"+{px - tw//2}+{py - th//2}")

    # Animación
    def fade_in(alpha=0.0):
        if alpha < 1.0:
            alpha += 0.2
            try: toast.attributes("-alpha", alpha)
            except: pass
            toast.after(20, lambda: fade_in(alpha))
        else:
            toast.after(duration, lambda: fade_out(1.0))

    def fade_out(alpha):
        if alpha > 0.0:
            alpha -= 0.2
            try: toast.attributes("-alpha", alpha)
            except: pass
            toast.after(20, lambda: fade_out(alpha))
        else:
            toast.destroy()

    fade_in()

def hex_to_rgb(hex_color):
    """Hex a RGB tuple."""
    hex_color = hex_color.lstrip('#')
    return tuple(int(hex_color[i:i+2], 16) for i in (0, 2, 4))
