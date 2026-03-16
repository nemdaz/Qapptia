import customtkinter as ctk
from PIL import Image, ImageDraw

def create_refresh_icon(color="white"):
    img = Image.new("RGBA", (24, 24), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    
    # Arco curvo (3/4 de círculo)
    d.arc([5, 5, 19, 19], start=0, end=270, fill=color, width=2)
    
    # Punta de flecha tangencial
    d.polygon([(18, 5), (12, 2), (12, 8)], fill=color)
    return img

def create_rotate_icon(color="white"):
    img = Image.new("RGBA", (24, 24), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    w = 2
    
    # 1. d="M14 20H17C19.2091 20 21 18.2091 21 16V12"
    d.line([(14, 20), (17, 20)], fill=color, width=w)
    d.arc([13, 12, 21, 20], start=0, end=90, fill=color, width=w)
    d.line([(21, 16), (21, 12)], fill=color, width=w)
    
    # 2. d="M16.5 22.5L14 20L16.5 17.5"
    d.line([(16.5, 22.5), (14, 20), (16.5, 17.5)], fill=color, width=w, joint="curve")
    
    # 3. d="M14 11L14 5C14 4.44772 13.5523 4 13 4L7 4"
    d.line([(14, 11), (14, 5)], fill=color, width=w)
    d.arc([12, 4, 14, 6], start=270, end=360, fill=color, width=w)
    d.line([(13, 4), (7, 4)], fill=color, width=w)
    
    # 4. d="M2 4H4"
    d.line([(2, 4), (4, 4)], fill=color, width=w)
    
    # 5. d="M14 16V14"
    d.line([(14, 16), (14, 14)], fill=color, width=w)
    
    # 6. d="M4 2L4 13C4 13.5523 4.44772 14 5 14L16 14"
    d.line([(4, 2), (4, 13)], fill=color, width=w)
    d.arc([4, 12, 6, 14], start=90, end=180, fill=color, width=w)
    d.line([(5, 14), (16, 14)], fill=color, width=w)
    
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
        
    if img:
        _icon_cache[cache_key] = img
        
    return img
