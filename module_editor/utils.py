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
    # Copia al portapapeles usando Win32 API con firmas estrictas (x64 compatible).
    if not pil_image: return False
    try:
        output = io.BytesIO()
        pil_image.convert("RGB").save(output, format="BMP")
        data = output.getvalue()[14:] # BITMAPINFOHEADER + Pixels
        output.close()

        CF_DIB = 8
        GMEM_MOVEABLE = 2
        user32 = ctypes.windll.user32
        kernel32 = ctypes.windll.kernel32

        # Definir firmas para evitar truncamiento de punteros en 64 bits
        kernel32.GlobalAlloc.argtypes = [wintypes.UINT, ctypes.c_size_t]
        kernel32.GlobalAlloc.restype = wintypes.HGLOBAL
        kernel32.GlobalLock.argtypes = [wintypes.HGLOBAL]
        kernel32.GlobalLock.restype = wintypes.LPVOID
        kernel32.GlobalUnlock.argtypes = [wintypes.HGLOBAL]
        kernel32.GlobalUnlock.restype = wintypes.BOOL
        user32.OpenClipboard.argtypes = [wintypes.HWND]
        user32.OpenClipboard.restype = wintypes.BOOL
        user32.EmptyClipboard.restype = wintypes.BOOL
        user32.SetClipboardData.argtypes = [wintypes.UINT, wintypes.HANDLE]
        user32.SetClipboardData.restype = wintypes.HANDLE
        user32.CloseClipboard.restype = wintypes.BOOL

        h_mem = kernel32.GlobalAlloc(GMEM_MOVEABLE, len(data))
        if not h_mem: return False
        p_mem = kernel32.GlobalLock(h_mem)
        if not p_mem: return False
        
        ctypes.memmove(p_mem, data, len(data))
        kernel32.GlobalUnlock(h_mem)

        if user32.OpenClipboard(None):
            try:
                user32.EmptyClipboard()
                if not user32.SetClipboardData(CF_DIB, h_mem):
                    # print("Error: SetClipboardData falló.")
                    pass
            finally:
                user32.CloseClipboard()
            return True
        return False
    except Exception as e:
        # print(f"Error nativo al copiar al clipboard: {e}")
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
    # Hex a RGB tuple.
    hex_color = hex_color.lstrip('#')
    return tuple(int(hex_color[i:i+2], 16) for i in (0, 2, 4))

def is_point_in_rect(px, py, x1, y1, x2, y2, tol=10, hollow=False):
    # Detección transversal de punto en rectángulo (con filtro estricto).
    min_x, max_x = min(x1, x2), max(x1, x2)
    min_y, max_y = min(y1, y2), max(y1, y2)
    if not (min_x-tol <= px <= max_x+tol and min_y-tol <= py <= max_y+tol): return False
    if not hollow: return True
    inner = (min_x+tol < px < max_x-tol) and (min_y+tol < py < max_y-tol)
    return not inner

def is_point_near_segment(px, py, x1, y1, x2, y2, tol=10):
    # Detección transversal de punto cerca de un segmento (con filtro de caja).
    min_x, max_x = min(x1, x2) - tol, max(x1, x2) + tol
    min_y, max_y = min(y1, y2) - tol, max(y1, y2) + tol
    if not (min_x <= px <= max_x and min_y <= py <= max_y): return False
    
    dx, dy = x2 - x1, y2 - y1
    l2 = dx*dx + dy*dy
    if l2 < 1e-6: return ((px-x1)**2 + (py-y1)**2)**0.5 < tol
    
    t = max(0, min(1, ((px - x1) * dx + (py - y1) * dy) / l2))
    return ((px - (x1 + t * dx))**2 + (py - (y1 + t * dy))**2)**0.5 < tol
