import customtkinter as ctk
from PIL import Image, ImageDraw

def create_refresh_icon(color="white"):
    img = Image.new("RGBA", (24, 24), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    w = 2
    
    # Cabeza de la flecha en la esquina superior derecha
    d.line([(21, 3), (21, 8)], fill=color, width=w, joint="curve")
    d.line([(21, 8), (16, 8)], fill=color, width=w, joint="curve")
    
    # Unión de la cabeza al arco
    d.line([(21, 8), (18, 5.3)], fill=color, width=w, joint="curve")
    
    # Arco abierto que rodea el centro
    # El SVG parte aproximadamente desde el ángulo 25° y muere en 315° (-45°)
    d.arc([3, 3, 21, 21], start=25, end=315, fill=color, width=w)
    
    return img
    
def create_rotate_icon(color="white"):
    img = Image.new("RGBA", (24, 24), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    w = 2
    d.line([(14, 20), (17, 20)], fill=color, width=w)
    d.arc([13, 12, 21, 20], start=0, end=90, fill=color, width=w)
    d.line([(21, 16), (21, 12)], fill=color, width=w)
    d.line([(16.5, 22.5), (14, 20), (16.5, 17.5)], fill=color, width=w, joint="curve")
    d.line([(14, 11), (14, 5)], fill=color, width=w)
    d.arc([12, 4, 14, 6], start=270, end=360, fill=color, width=w)
    d.line([(13, 4), (7, 4)], fill=color, width=w)
    d.line([(2, 4), (4, 4)], fill=color, width=w)
    d.line([(14, 16), (14, 14)], fill=color, width=w)
    d.line([(4, 2), (4, 13)], fill=color, width=w)
    d.arc([4, 12, 6, 14], start=90, end=180, fill=color, width=w)
    d.line([(5, 14), (16, 14)], fill=color, width=w)
    return img

def create_arrow_icon(color="white"):
    img = Image.new("RGBA", (24, 24), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    w = 2
    # d="M19 13v6m0 0h-6m6 0L5 5" (Modificamos 19,13 por algo visualmente centrado)
    d.line([(5, 5), (19, 19)], fill=color, width=w) # L5 5 -> 19 19
    d.line([(19, 13), (19, 19)], fill=color, width=w) # v6
    d.line([(13, 19), (19, 19)], fill=color, width=w) # h-6
    return img

def create_rect_icon(color="white"):
    img = Image.new("RGBA", (24, 24), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([3, 5, 21, 20], radius=2, outline=color, width=2)
    return img

def create_copy_file_icon(color="white"):
    img = Image.new("RGBA", (24, 24), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    w = 2
    # Documento trasero
    d.line([(3.5, 10), (3.5, 22), (14, 22)], fill=color, width=w, joint="curve")
    d.line([(9.5, 10), (11.5, 10)], fill=color, width=w)
    d.line([(9.5, 14), (15.5, 14)], fill=color, width=w)
    # Documento frontal
    d.polygon([(6.5, 19), (20.5, 19), (20.5, 8), (15, 8), (15, 2), (6.5, 2)], outline=color, width=w)
    d.line([(15, 2), (20.5, 8)], fill=color, width=w) # Doblez
    return img

def create_copy_clipboard_icon(color="white"):
    img = Image.new("RGBA", (24, 24), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    w = 2

    # Clip superior (Grapadora del portapapeles)
    d.rounded_rectangle([9, 3, 15, 7], radius=2, outline=color, width=w)
    
    # Contorno Izquierdo de la Tabla
    d.line([(9, 5), (7, 5)], fill=color, width=w)
    d.arc([5, 5, 9, 9], start=180, end=270, fill=color, width=w)  # curva sup-izq
    d.line([(5, 7), (5, 19)], fill=color, width=w)
    d.arc([5, 17, 9, 21], start=90, end=180, fill=color, width=w) # curva inf-izq
    d.line([(7, 21), (12, 21)], fill=color, width=w)
    
    # Contorno Derecho de la Tabla
    d.line([(15, 5), (17, 5)], fill=color, width=w)
    d.arc([15, 5, 19, 9], start=270, end=360, fill=color, width=w) # curva sup-der
    d.line([(19, 7), (19, 13)], fill=color, width=w)
    
    # Símbolo Copy interno (L invertida atrás, rounded rect adelante)
    # L atrás:
    d.line([(13, 19), (13, 14)], fill=color, width=w)
    d.arc([13, 13, 15, 15], start=180, end=270, fill=color, width=w) # curva de L
    d.line([(14, 13), (19, 13)], fill=color, width=w)
    # Rounded rect frontal con esquinas precisas:
    d.rounded_rectangle([15, 15, 21, 21], radius=1, outline=color, width=w)
    
    return img

def create_save_icon(color="white"):
    img = Image.new("RGBA", (24, 24), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    
    # Borde de disquete
    d.rectangle([4, 4, 20, 20], outline=color, width=2)
    # Etiqueta superior
    d.rectangle([8, 4, 16, 10], fill=color)
    # Slider metálico inferior
    d.rectangle([7, 14, 17, 20], outline=color, width=2)
    return img

def create_image_file_icon(color="white"):
    img = Image.new("RGBA", (24, 24), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    # Borde de documento
    d.polygon([(4, 2), (14, 2), (20, 8), (20, 22), (4, 22)], outline=color, width=2)
    # Doblez hoja
    d.line([(14, 2), (14, 8), (20, 8)], fill=color, width=2)
    # Dibujo de montañas interior
    d.polygon([(6, 18), (10, 13), (15, 18)], fill=color)
    d.polygon([(11, 18), (14, 15), (18, 18)], fill=color)
    d.ellipse([(14, 10), (16, 12)], fill=color)
    return img

def create_folder_icon(color="white"):
    img = Image.new("RGBA", (24, 24), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    w = 2
    
    # Contorno trasero
    back_points = [
        (5, 19), (3, 17), (3, 6), (5, 4), (9, 4), (12, 7), (19, 7), (21, 9), (21, 11)
    ]
    d.line(back_points, fill=color, width=w, joint="curve")
    
    # Solapa frontal
    front_points = [
        (5, 19), (7.8, 11.6), (8.7, 11), (21, 11), (22, 12.2), (21, 17.4), (19, 19), (5, 19)
    ]
    d.line(front_points, fill=color, width=w, joint="curve")
    
    return img

_refresh_img = create_refresh_icon()
_rotate_img = create_rotate_icon()
_save_img = create_save_icon()
_image_file_img = create_image_file_icon()
_folder_img = create_folder_icon()
_arrow_img = create_arrow_icon()
_rect_img = create_rect_icon()
_copy_file_img = create_copy_file_icon()
_copy_clip_img = create_copy_clipboard_icon()

_icon_cache = {}

def get_icon(name, size=(20, 20)):
    cache_key = (name, size)
    if cache_key in _icon_cache:
        return _icon_cache[cache_key]
        
    img = None
    if name == "refresh":
        img = ctk.CTkImage(light_image=_refresh_img, dark_image=_refresh_img, size=size)
    elif name == "rotate":
        img = ctk.CTkImage(light_image=_rotate_img, dark_image=_rotate_img, size=size)
    elif name == "save":
        img = ctk.CTkImage(light_image=_save_img, dark_image=_save_img, size=size)
    elif name == "image_file":
        img = ctk.CTkImage(light_image=_image_file_img, dark_image=_image_file_img, size=size)
    elif name == "folder":
        img = ctk.CTkImage(light_image=_folder_img, dark_image=_folder_img, size=size)
    elif name == "arrow":
        img = ctk.CTkImage(light_image=_arrow_img, dark_image=_arrow_img, size=size)
    elif name == "rect":
        img = ctk.CTkImage(light_image=_rect_img, dark_image=_rect_img, size=size)
    elif name == "copy_file":
        img = ctk.CTkImage(light_image=_copy_file_img, dark_image=_copy_file_img, size=size)
    elif name == "copy_clip":
        img = ctk.CTkImage(light_image=_copy_clip_img, dark_image=_copy_clip_img, size=size)
        
    if img:
        _icon_cache[cache_key] = img
        
    return img
