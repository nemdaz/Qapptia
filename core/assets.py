import customtkinter as ctk
from PIL import Image, ImageDraw

def create_rotate_icon(color="white"):
    img = Image.new("RGBA", (24, 24), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    
    # Arco curvo (3/4 de círculo)
    d.arc([5, 5, 19, 19], start=0, end=270, fill=color, width=2)
    
    # Punta de flecha tangencial
    d.polygon([(18, 5), (12, 2), (12, 8)], fill=color)
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

_rotate_img = create_rotate_icon()
_save_img = create_save_icon()

def get_icon(name, size=(20, 20)):
    if name == "rotate":
        return ctk.CTkImage(light_image=_rotate_img, dark_image=_rotate_img, size=size)
    elif name == "save":
        return ctk.CTkImage(light_image=_save_img, dark_image=_save_img, size=size)
    return None
