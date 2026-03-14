import customtkinter as ctk
import config
from tkinter import filedialog

ctk.set_appearance_mode("Dark")
ctk.set_default_color_theme("blue")

class ConfigApp(ctk.CTk):
    def __init__(self, on_close_callback=None):
        super().__init__()
        self.on_close_callback = on_close_callback
        
        self.title("Configuración de Capturador")
        self.geometry("540x480")
        self.resizable(False, False)
        
        # Tabs
        self.tabview = ctk.CTkTabview(self)
        self.tabview.pack(padx=20, pady=10, fill="both", expand=True)
        
        self.tab_general = self.tabview.add("General")
        self.tab_capturas = self.tabview.add("Capturas")
        
        self.setup_general_tab()
        self.setup_capturas_tab()
        
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
        
        # Support default migration from old prefix to format
        val = config.get("filename_format")
        if not val: # Fallback al viejo prefix temporal
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
        
    def show_format_help(self):
        help_win = ctk.CTkToplevel(self)
        help_win.title("Ayuda de Formato")
        
        # Tamaño incrementado para que el texto encaje perfectamente
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
        
        # Usar transiet para ocultar botón maximizar e iconificar 
        # sin deformar los pixeles del botón de cerrar 
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
        # --- Modo Manual ---
        frame_manual = ctk.CTkFrame(self.tab_capturas)
        frame_manual.pack(fill="x", padx=10, pady=5)
        ctk.CTkLabel(frame_manual, text="Modo Manual", font=("Arial", 14, "bold")).pack(anchor="w", padx=10, pady=(5,0))
        
        container_manual = ctk.CTkFrame(frame_manual, fg_color="transparent")
        container_manual.pack(fill="x", padx=10, pady=5)
        
        ctk.CTkLabel(container_manual, text="Timer (segs):").grid(row=0, column=0, padx=10, pady=5, sticky="w")
        self.entry_timer = ctk.CTkEntry(container_manual, width=100)
        self.entry_timer.insert(0, str(config.get("manual_timer")))
        self.entry_timer.grid(row=0, column=1, padx=10, pady=5, sticky="w")
        
        # --- Modo Atajo ---
        frame_atajo = ctk.CTkFrame(self.tab_capturas)
        frame_atajo.pack(fill="x", padx=10, pady=5)
        ctk.CTkLabel(frame_atajo, text="Modo Atajo", font=("Arial", 14, "bold")).pack(anchor="w", padx=10, pady=(5,0))
        
        container_atajo = ctk.CTkFrame(frame_atajo, fg_color="transparent")
        container_atajo.pack(fill="x", padx=10, pady=5)
        
        ctk.CTkLabel(container_atajo, text="Combinación Global:").grid(row=0, column=0, padx=10, pady=5, sticky="w")
        self.entry_shortcut = ctk.CTkEntry(container_atajo, width=150)
        
        shortcut_val = config.get("shortcut_key") or "ctrl+shift+k"
        self.entry_shortcut.insert(0, str(shortcut_val).upper())
        self.entry_shortcut.grid(row=0, column=1, padx=10, pady=5, sticky="w")
        self.enable_shortcut_recording(self.entry_shortcut, 3)
        
        self.btn_reset_atajo = ctk.CTkButton(container_atajo, text="Restablecer", width=80, command=self.reset_atajo)
        self.btn_reset_atajo.grid(row=0, column=2, padx=10, pady=5)
        
        # --- Modo Flujo ---
        frame_flujo = ctk.CTkFrame(self.tab_capturas)
        frame_flujo.pack(fill="x", padx=10, pady=5)
        ctk.CTkLabel(frame_flujo, text="Modo Flujo", font=("Arial", 14, "bold")).pack(anchor="w", padx=10, pady=(5,0))
        
        container_flujo = ctk.CTkFrame(frame_flujo, fg_color="transparent")
        container_flujo.pack(fill="x", padx=10, pady=5)
        
        ctk.CTkLabel(container_flujo, text="Pausa Temporal (Teclas):").grid(row=0, column=0, padx=10, pady=5, sticky="w")
        self.entry_pause = ctk.CTkEntry(container_flujo, width=150)
        
        pause_val = config.get("flow_pause_key")
        if pause_val is None or pause_val == "":
            pause_val = "CTRL+SHIFT"
            
        self.entry_pause.insert(0, str(pause_val).upper())
        self.entry_pause.grid(row=0, column=1, padx=10, pady=5, sticky="w")
        self.enable_shortcut_recording(self.entry_pause, 2)
        
        self.btn_clear_pause = ctk.CTkButton(container_flujo, text="Limpiar", width=60, command=self.clear_pause)
        self.btn_clear_pause.grid(row=0, column=2, padx=10, pady=5)

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
        self.entry_shortcut.insert(0, "CTRL+SHIFT+K")
        self.entry_shortcut._recorded_keys = ["ctrl", "shift", "k"]

    def enable_shortcut_recording(self, entry, max_keys):
        entry._recorded_keys = []
        
        def on_focus(e):
            entry._recorded_keys = []
            entry.delete(0, 'end')
            
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
        entry.bind("<KeyPress>", on_key)
        
    def save_and_close(self):
        config.set("save_path", self.entry_path.get())
        config.set("filename_format", self.entry_prefix.get())
        config.set("image_quality", int(self.slider_quality.get()))
        
        config.set("subfolder_month", bool(self.chk_month.get()))
        config.set("subfolder_day", bool(self.chk_day.get()))
        config.set("subfolder_hour", bool(self.chk_hour.get()))
        
        try:
            config.set("manual_timer", int(self.entry_timer.get()))
        except ValueError:
            pass
            
        config.set("shortcut_key", self.entry_shortcut.get().lower())
        config.set("flow_pause_key", self.entry_pause.get().lower())
        
        if self.on_close_callback:
            self.on_close_callback()
            
        self.destroy()

def run_gui(on_close_callback=None):
    app = ConfigApp(on_close_callback)
    app.mainloop()

if __name__ == "__main__":
    run_gui()
