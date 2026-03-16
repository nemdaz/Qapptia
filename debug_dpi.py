import mouse
import tkinter as tk
import ctypes

def get_tk_info():
    root = tk.Tk()
    w = root.winfo_screenwidth()
    h = root.winfo_screenheight()
    root.destroy()
    return w, h

def get_ctypes_info():
    user32 = ctypes.windll.user32
    return user32.GetSystemMetrics(0), user32.GetSystemMetrics(1)

def get_physical_info():
    user32 = ctypes.windll.user32
    user32.SetProcessDPIAware()
    return user32.GetSystemMetrics(0), user32.GetSystemMetrics(1)

print(f"Tkinter Logic: {get_tk_info()}")
print(f"Ctypes Metrics: {get_ctypes_info()}")
print(f"Ctypes Physical: {get_physical_info()}")
print(f"Mouse Position: {mouse.get_position()}")
