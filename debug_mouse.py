import mouse
import time
import tkinter as tk

def get_screen_info():
    try:
        root = tk.Tk()
        w = root.winfo_screenwidth()
        h = root.winfo_screenheight()
        root.destroy()
        return w, h
    except:
        return 1920, 1080

def on_event(event):
    ev_type = type(event).__name__
    if ev_type == 'ButtonEvent':
        print(f"Boton: {event.button} | Tipo: {event.event_type} | Time: {event.time}")
        mx, my = mouse.get_position()
        print(f" -> Posicion al clic: {mx}, {my}")
    elif ev_type == 'WheelEvent':
        print(f"Rueda: {event.delta} | Time: {event.time}")
    elif ev_type == 'MoveEvent':
        # Demasiados eventos de movimiento, solo imprimir si es extremo
        if event.x > 1800:
            print(f"Movimiento Extremo: {event.x}, {event.y}")

sw, sh = get_screen_info()
print(f"Resolucion detectada: {sw}x{sh}")
print(f"Zona de scroll (94%): > {sw * 0.94}")

print("Escuchando eventos de mouse durante 10 segundos... Haz clic en el borde derecho.")
mouse.hook(on_event)
time.sleep(10)
mouse.unhook_all()
print("Fin de prueba.")
