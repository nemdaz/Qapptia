import customtkinter as ctk
import sys
import os

# Configurar ruta base del proyecto
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from core import config
from tkinter import filedialog

ctk.set_appearance_mode("Dark")
ctk.set_default_color_theme("blue")

class ConfigApp(ctk.CTk):
    def __init__(self, on_close_callback=None):
        super().__init__()
        self.on_close_callback = on_close_callback
        
        self.title("Configuración de Capturador")
        self.geometry("600x520")
        self.resizable(False, False)
        
        # Tabs
        self.tabview = ctk.CTkTabview(self)
        self.tabview.pack(padx=20, pady=10, fill="both", expand=True)
        
        self.tab_general = self.tabview.add("General")
        self.tab_capturas = self.tabview.add("Capturas")
        
        self.setup_general_tab()
        self.setup_capturas_tab()
        
        # Forzar pérdida de foco al hacer clic en fondo/etiquetas
        def on_bg_click(e):
            # Evitar interferencia con el foco al entrar al input
            if "entry" not in str(e.widget).lower():
                self.focus_set()
        
        self.bind("<Button-1>", on_bg_click)
        
        # Save button
        self.btn_save = ctk.CTkButton(self, text="Guardar y Cerrar", command=self.save_and_close)
        self.btn_save.pack(pady=10)
        
    def setup_general_tab(self):
        # Ruta
        ctk.CTkLabel(self.tab_general, text="Ruta de Guardado:").grid(row=0, column=0, padx=10, pady=10, sticky="w")
        self.entry_path = ctk.CTkEntry(self.tab_general, width=200)
        self.entry_path.insert(0, config.get("save_path"))
        self.entry_path.grid(row=0, column=1, padx=10, pady=10)
        
        self.btn_browse = ctk.CTkButton(self.tab_general, text="Examinar", width=80, command=self.browse_path)
        self.btn_browse.grid(row=0, column=2, padx=10, pady=10)
        
        # Prefijo/Formato
        ctk.CTkLabel(self.tab_general, text="Formato de Nombre:").grid(row=1, column=0, padx=10, pady=10, sticky="w")
        self.entry_prefix = ctk.CTkEntry(self.tab_general, width=200, placeholder_text="Screenshot_YYYYMMDD_HHmmSS")
        
        # Migración de formato de nombre
        val = config.get("filename_format")
        if not val:
            old_prefix = config.get("filename_prefix") or "screenshot_"
            val = old_prefix + "YYYYMMDD_HHmmSS"
            
        self.entry_prefix.insert(0, val)
        self.entry_prefix.grid(row=1, column=1, padx=10, pady=10, sticky="w")
        
        # Botón de Ayuda (?)
        self.btn_help = ctk.CTkButton(self.tab_general, text="(?)", width=30, command=self.show_format_help)
        self.btn_help.grid(row=1, column=2, padx=(0, 10), pady=10, sticky="w")
        
        # Calidad
        ctk.CTkLabel(self.tab_general, text="Calidad JPG/PNG:").grid(row=2, column=0, padx=10, pady=10, sticky="w")
        self.slider_quality = ctk.CTkSlider(self.tab_general, from_=10, to=100, number_of_steps=90)
        self.slider_quality.set(config.get("image_quality"))
        self.slider_quality.grid(row=2, column=1, padx=10, pady=10, columnspan=2, sticky="w")
        
        # Subcarpetas
        ctk.CTkLabel(self.tab_general, text="Organizar en subcarpetas:").grid(row=3, column=0, padx=10, pady=10, sticky="nw")
        
        frame_subs = ctk.CTkFrame(self.tab_general, fg_color="transparent")
        frame_subs.grid(row=3, column=1, columnspan=2, padx=10, pady=10, sticky="w")
        
        self.chk_month = ctk.CTkCheckBox(frame_subs, text="Por Mes (YYYY-MM)")
        self.chk_month.pack(anchor="w", pady=2)
        if bool(config.get("subfolder_month")): 
            self.chk_month.select()
        
        self.chk_day = ctk.CTkCheckBox(frame_subs, text="Por Día (YYYY-MM-DD)")
        self.chk_day.pack(anchor="w", pady=2)
        if bool(config.get("subfolder_day")): 
            self.chk_day.select()
        
        self.chk_hour = ctk.CTkCheckBox(frame_subs, text="Por Hora (YYYY-MM-DD HH)")
        self.chk_hour.pack(anchor="w", pady=2)
        if bool(config.get("subfolder_hour")): 
            self.chk_hour.select()
            
        # Opciones del Mouse
        ctk.CTkLabel(self.tab_general, text="Cursor:").grid(row=4, column=0, padx=10, pady=10, sticky="nw")
        
        frame_mouse = ctk.CTkFrame(self.tab_general, fg_color="transparent")
        frame_mouse.grid(row=4, column=1, columnspan=2, padx=10, pady=10, sticky="w")
        
        self.chk_mouse = ctk.CTkCheckBox(frame_mouse, text="Capturar", command=self.toggle_highlight_state)
        self.chk_mouse.pack(anchor="w", pady=2)
        if bool(config.get("show_mouse")): 
            self.chk_mouse.select()
            
        self.chk_highlight = ctk.CTkCheckBox(frame_mouse, text="Resaltar (Halo)")
        self.chk_highlight.pack(anchor="w", pady=2)
        if bool(config.get("highlight_mouse")): 
            self.chk_highlight.select()
        else:
            self.chk_highlight.deselect()
            
        self.toggle_highlight_state() # Init state visual
        
    def toggle_highlight_state(self):
        if self.chk_mouse.get() == 1:
            self.chk_highlight.configure(state="normal")
        else:
            self.chk_highlight.configure(state="disabled")
            self.chk_highlight.deselect()
        
    def show_format_help(self):
        help_win = ctk.CTkToplevel(self)
        help_win.title("Ayuda de Formato")
        
        # Dimensiones optimizadas para el mensaje de ayuda
        win_width = 350
        win_height = 200
        
        # Centrar relativo a la ventana principal
        self.update_idletasks()
        main_x = self.winfo_rootx()
        main_y = self.winfo_rooty()
        main_width = self.winfo_width()
        main_height = self.winfo_height()
        
        pos_x = main_x + (main_width // 2) - (win_width // 2)
        pos_y = main_y + (main_height // 2) - (win_height // 2)
        
        help_win.geometry(f"{win_width}x{win_height}+{pos_x}+{pos_y}")
        help_win.resizable(False, False)
        
        # Vinculación modal con ventana principal
        help_win.transient(self)
        
        # Asegurar que está por encima y enfocada
        help_win.attributes("-topmost", True)
        help_win.focus_force()
        
        msg = ("Usa los siguientes valores para formatear la fecha:\n\n"
               "YYYY = Año (4 dígitos)\n"
               "MM = Mes (2 dígitos)\n"
               "DD = Día (2 dígitos)\n"
               "HH = Horas\n"
               "mm = Minutos\n"
               "SS = Segundos")
               
        lbl = ctk.CTkLabel(help_win, text=msg, justify="left")
        lbl.pack(padx=20, pady=20)
        
    def setup_capturas_tab(self):
        LBL_W = 100
        ATAJO_W = 150
        TIMER_W = 80

        # --- Modo Pantalla ---
        frame_pantalla = ctk.CTkFrame(self.tab_capturas)
        frame_pantalla.pack(fill="x", padx=10, pady=5)
        ctk.CTkLabel(frame_pantalla, text="Modo Pantalla", font=("Arial", 14, "bold")).pack(anchor="w", padx=10, pady=(5,0))
        
        container_p = ctk.CTkFrame(frame_pantalla, fg_color="transparent")
        container_p.pack(fill="x", padx=10, pady=5)
        
        # Fila 0: Atajo
        ctk.CTkLabel(container_p, text="Atajo:", width=LBL_W, anchor="w").grid(row=0, column=0, padx=5, pady=5, sticky="w")
        self.entry_shortcut = ctk.CTkEntry(container_p, width=ATAJO_W)
        shortcut_val = config.get("shortcut_screen") or "ctrl+shift+q"
        self.entry_shortcut.insert(0, str(shortcut_val).upper())
        self.entry_shortcut.grid(row=0, column=1, padx=5, pady=5, sticky="w")
        self.enable_shortcut_recording(self.entry_shortcut, 3)
        
        # Fila 1: Timer
        ctk.CTkLabel(container_p, text="Timer (s):", width=LBL_W, anchor="w").grid(row=1, column=0, padx=5, pady=5, sticky="w")
        self.entry_timer = ctk.CTkEntry(container_p, width=TIMER_W)
        self.entry_timer.insert(0, str(config.get("manual_timer")))
        self.entry_timer.grid(row=1, column=1, padx=5, pady=5, sticky="w")
        
        # --- Modo Area ---
        frame_area = ctk.CTkFrame(self.tab_capturas)
        frame_area.pack(fill="x", padx=10, pady=5)
        ctk.CTkLabel(frame_area, text="Modo Area", font=("Arial", 14, "bold")).pack(anchor="w", padx=10, pady=(5,0))
        
        container_a = ctk.CTkFrame(frame_area, fg_color="transparent")
        container_a.pack(fill="x", padx=10, pady=5)
        
        # Fila 0: Atajo
        ctk.CTkLabel(container_a, text="Atajo:", width=LBL_W, anchor="w").grid(row=0, column=0, padx=5, pady=5, sticky="w")
        self.entry_shortcut_area = ctk.CTkEntry(container_a, width=ATAJO_W)
        shortcut_area_val = config.get("shortcut_area") or "ctrl+shift+a"
        self.entry_shortcut_area.insert(0, str(shortcut_area_val).upper())
        self.entry_shortcut_area.grid(row=0, column=1, padx=5, pady=5, sticky="w")
        self.enable_shortcut_recording(self.entry_shortcut_area, 3)
        
        # --- Modo Flujo ---
        frame_flujo = ctk.CTkFrame(self.tab_capturas)
        frame_flujo.pack(fill="x", padx=10, pady=5)
        ctk.CTkLabel(frame_flujo, text="Modo Flujo", font=("Arial", 14, "bold")).pack(anchor="w", padx=10, pady=(5,0))
        
        container_f = ctk.CTkFrame(frame_flujo, fg_color="transparent")
        container_f.pack(fill="x", padx=10, pady=5)
        
        # Fila 0: Atajo principal (Toggle)
        ctk.CTkLabel(container_f, text="Atajo:", width=LBL_W, anchor="w").grid(row=0, column=0, padx=5, pady=5, sticky="w")
        self.entry_shortcut_flow = ctk.CTkEntry(container_f, width=ATAJO_W)
        shortcut_flow_val = config.get("shortcut_flow") or "ctrl+shift+f"
        self.entry_shortcut_flow.insert(0, str(shortcut_flow_val).upper())
        self.entry_shortcut_flow.grid(row=0, column=1, padx=5, pady=5, sticky="w")
        self.enable_shortcut_recording(self.entry_shortcut_flow, 3)

        # Fila 1: Atajo Pausa
        ctk.CTkLabel(container_f, text="Pausa:", width=LBL_W, anchor="w").grid(row=1, column=0, padx=5, pady=5, sticky="w")
        self.entry_pause = ctk.CTkEntry(container_f, width=ATAJO_W)
        pause_val = config.get("shortcut_flow_pause") or "CTRL+SHIFT"
        self.entry_pause.insert(0, str(pause_val).upper())
        self.entry_pause.grid(row=1, column=1, padx=5, pady=5, sticky="w")
        self.enable_shortcut_recording(self.entry_pause, 2)
        
        # Fila 2: Opción de Scroll
        self.chk_enable_scroll = ctk.CTkCheckBox(container_f, text="Habilitar Captura de Scroll Inteligente")
        self.chk_enable_scroll.grid(row=2, column=0, columnspan=2, padx=15, pady=(10, 5), sticky="w")
        if bool(config.get("enable_scroll_capture")):
            self.chk_enable_scroll.select()
        else:
            self.chk_enable_scroll.deselect()

    def browse_path(self):
        path = filedialog.askdirectory()
        if path:
            self.entry_path.delete(0, 'end')
            self.entry_path.insert(0, path)
            
    def clear_pause(self):
        self.entry_pause.delete(0, 'end')
        if hasattr(self.entry_pause, '_recorded_keys'):
            self.entry_pause._recorded_keys = []
        
    def reset_atajo(self):
        self.entry_shortcut.delete(0, 'end')
        self.entry_shortcut.insert(0, "CTRL+SHIFT+Q")
        self.entry_shortcut._recorded_keys = ["ctrl", "shift", "q"]

    def enable_shortcut_recording(self, entry, max_keys):
        entry._recorded_keys = []
        entry._prev_val = ""
        
        def on_focus(e):
            # Respaldar valor actual antes de limpiar
            current = entry.get()
            if current and current != "Presiona teclas...":
                entry._prev_val = current
            entry._recorded_keys = []
            entry.delete(0, 'end')
            entry.configure(placeholder_text="Presiona teclas...")
        
        def on_focus_out(e):
            # Restaurar previo si se pierde foco sin cambios
            def restore_if_empty():
                if not entry.get() or not entry._recorded_keys:
                    entry.delete(0, 'end')
                    entry.insert(0, entry._prev_val)
                    entry.configure(placeholder_text="")
            
            # Retardo para asegurar procesamiento de eventos globales
            entry.after(100, restore_if_empty)
            
        def on_key(e):
            if e.keysym.lower() == 'tab':
                return
                
            if e.keysym.lower() == 'backspace':
                entry._recorded_keys = []
                entry.delete(0, 'end')
                return "break"
                
            sym = e.keysym.lower()
            if 'control' in sym: sym = 'ctrl'
            elif 'shift' in sym: sym = 'shift'
            elif 'alt' in sym: sym = 'alt'
            elif 'win' in sym or 'super' in sym: sym = 'windows'
            
            if sym not in entry._recorded_keys and len(entry._recorded_keys) < max_keys:
                entry._recorded_keys.append(sym)
                val = "+".join(entry._recorded_keys).upper()
                entry.delete(0, 'end')
                entry.insert(0, val)
                
            return "break"
            
        entry.bind("<FocusIn>", on_focus)
        entry.bind("<FocusOut>", on_focus_out)
        entry.bind("<KeyPress>", on_key)
        
    def save_and_close(self):
        config.set("save_path", self.entry_path.get())
        config.set("filename_format", self.entry_prefix.get())
        config.set("image_quality", int(self.slider_quality.get()))
        
        config.set("subfolder_month", bool(self.chk_month.get()))
        config.set("subfolder_day", bool(self.chk_day.get()))
        config.set("subfolder_hour", bool(self.chk_hour.get()))
        
        config.set("show_mouse", bool(self.chk_mouse.get()))
        config.set("highlight_mouse", bool(self.chk_highlight.get()))
        
        try:
            config.set("manual_timer", int(self.entry_timer.get()))
        except ValueError:
            pass
            
        config.set("shortcut_screen", self.entry_shortcut.get().lower())
        config.set("shortcut_area", self.entry_shortcut_area.get().lower())
        config.set("shortcut_flow", self.entry_shortcut_flow.get().lower())
        config.set("shortcut_flow_pause", self.entry_pause.get().lower())
        config.set("enable_scroll_capture", bool(self.chk_enable_scroll.get()))
        
        if self.on_close_callback:
            self.on_close_callback()
            
        self.destroy()

def run_gui(on_close_callback=None):
    app = ConfigApp(on_close_callback)
    app.mainloop()

if __name__ == "__main__":
    run_gui()
