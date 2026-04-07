import os

from PIL import Image, ImageDraw, ImageFont


ASSETS_DIR = os.path.join(os.path.dirname(__file__), "assets")
FONTS_DIR = os.path.join(ASSETS_DIR, "fonts")


def get_text_font_path(filename):
    return os.path.join(FONTS_DIR, filename)


def create_app_icon_image(size=64):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    pad = max(2, size // 10)
    radius = max(4, size // 6)
    draw.rounded_rectangle(
        [pad, pad, size - pad, size - pad],
        radius=radius,
        fill="#1f2933",
        outline="#f5f7fa",
        width=max(2, size // 16),
    )

    font_px = int(size * 0.56)
    font = None
    try:
        from module_editor import constants as editor_constants

        font_path = get_text_font_path(editor_constants.TEXT_STYLE["font_files"]["bold"])
        font = ImageFont.truetype(font_path, font_px)
    except Exception:
        font = None

    if font is None:
        try:
            font = ImageFont.truetype("arial.ttf", font_px)
        except Exception:
            font = ImageFont.load_default()

    text = "Q"
    box = draw.textbbox((0, 0), text, font=font)
    text_w = box[2] - box[0]
    text_h = box[3] - box[1]
    text_x = (size - text_w) // 2 - box[0]
    text_y = (size - text_h) // 2 - box[1] - max(0, size // 40)
    draw.text((text_x, text_y), text, font=font, fill="#ffffff")

    return img

def create_refresh_icon(color="white"):
    img = Image.new("RGBA", (24, 24), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    w = 2
    
    # Cabeza de flecha
    d.line([(21, 3), (21, 8)], fill=color, width=w, joint="curve")
    d.line([(21, 8), (16, 8)], fill=color, width=w, joint="curve")
    
    # Unión al arco
    d.line([(21, 8), (18, 5.3)], fill=color, width=w, joint="curve")
    
    # Arco abierto
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
    # Cuerpo de flecha
    d.line([(5, 5), (19, 19)], fill=color, width=w)
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
    # Doc trasero
    d.line([(3.5, 10), (3.5, 22), (14, 22)], fill=color, width=w, joint="curve")
    d.line([(9.5, 10), (11.5, 10)], fill=color, width=w)
    d.line([(9.5, 14), (15.5, 14)], fill=color, width=w)
    # Doc frontal
    d.polygon([(6.5, 19), (20.5, 19), (20.5, 8), (15, 8), (15, 2), (6.5, 2)], outline=color, width=w)
    d.line([(15, 2), (20.5, 8)], fill=color, width=w) # Doblez
    return img

def create_copy_clipboard_icon(color="white"):
    img = Image.new("RGBA", (24, 24), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    w = 2

    # Clip superior
    d.rounded_rectangle([9, 3, 15, 7], radius=2, outline=color, width=w)
    
    # Contorno izquierdo
    d.line([(9, 5), (7, 5)], fill=color, width=w)
    d.arc([5, 5, 9, 9], start=180, end=270, fill=color, width=w)  # curva sup-izq
    d.line([(5, 7), (5, 19)], fill=color, width=w)
    d.arc([5, 17, 9, 21], start=90, end=180, fill=color, width=w) # curva inf-izq
    d.line([(7, 21), (12, 21)], fill=color, width=w)
    
    # Contorno derecho
    d.line([(15, 5), (17, 5)], fill=color, width=w)
    d.arc([15, 5, 19, 9], start=270, end=360, fill=color, width=w) # curva sup-der
    d.line([(19, 7), (19, 13)], fill=color, width=w)
    
    # Símbolo Copy
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
    
    # Borde disquete
    d.rectangle([4, 4, 20, 20], outline=color, width=2)
    # Etiqueta
    d.rectangle([8, 4, 16, 10], fill=color)
    # Slider
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

def create_folder_collapsed_icon(color="white"):
    img = Image.new("RGBA", (24, 24), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    w = 2
    
    # SVG Path: M3 7 C3 5.89... H5 5 H7.7... C8.4... 9.25 6.25 ...
    # Re-dibujado para Pillow considerando w=2
    # Cuerpo y Pestaña integrados:
    path = [
        (3, 17.5), (3, 7), (3, 5), (5, 5), (7.7, 5), (9.25, 6.25), 
        (10.7, 7.5), (19, 7.5), (21, 7.5), (21, 9.5), (21, 17.5), (21, 19.5), (5, 19.5), (3, 19.5)
    ]
    # Dibujamos el contorno usando rounded_rectangle para las partes base y líneas para la pestaña
    d.rounded_rectangle([3, 7.5, 21, 19.5], radius=2, outline=color, width=w)
    # Pestaña superior
    d.line([(3, 7.5), (3, 5), (7.7, 5), (9.25, 6.25), (10.7, 7.5)], fill=color, width=w, joint="curve")
    
    return img

def create_color_square_icon(hex_color, size=(24, 24), padding=4):
    """Icono de color sólido."""
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    
    # Cuadrado con borde
    x1, y1 = padding, padding
    x2, y2 = size[0] - padding, size[1] - padding
    draw.rounded_rectangle([x1, y1, x2, y2], radius=3, fill=hex_color, outline="#444444", width=1)
    
    return img

def create_highlighter_icon(color="white"):
    img = Image.new("RGBA", (24, 24), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    w = 2
    # Cuerpo del resaltador
    d.polygon([(16, 4), (20, 8), (10, 18), (6, 14)], outline=color, width=w)
    # Punta gruesa
    d.polygon([(6, 14), (10, 18), (8, 20), (4, 16)], fill=color)
    return img


def create_text_icon(color="white"):
    img = Image.new("RGBA", (24, 24), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    d.rectangle([4, 4, 20, 20], outline=color, width=2)
    d.line([(8, 8), (16, 8)], fill=color, width=2)
    d.line([(12, 8), (12, 16)], fill=color, width=2)
    return img

def create_image_fit_icon(color="white"):
    img = Image.new("RGBA", (24, 24), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    w = 2

    d.rectangle([5, 5, 19, 19], outline=color, width=1)
    d.line([(12, 10), (12, 4)], fill=color, width=w)
    d.polygon([(12, 2), (9, 6), (15, 6)], fill=color)
    d.line([(12, 14), (12, 20)], fill=color, width=w)
    d.polygon([(12, 22), (9, 18), (15, 18)], fill=color)
    d.line([(10, 12), (4, 12)], fill=color, width=w)
    d.polygon([(2, 12), (6, 9), (6, 15)], fill=color)
    d.line([(14, 12), (20, 12)], fill=color, width=w)
    d.polygon([(22, 12), (18, 9), (18, 15)], fill=color)
    
    return img

def create_image_real_size_icon(color="white"):
    img = Image.new("RGBA", (24, 24), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    w = 2

    d.line([(4, 9), (4, 4), (9, 4)], fill=color, width=w, joint="curve")
    d.line([(15, 4), (20, 4), (20, 9)], fill=color, width=w, joint="curve")
    d.line([(4, 15), (4, 20), (9, 20)], fill=color, width=w, joint="curve")
    d.line([(15, 20), (20, 20), (20, 15)], fill=color, width=w, joint="curve")
    d.rectangle([8, 8, 16, 16], outline=color, width=2)

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
_folder_collapsed_img = create_folder_collapsed_icon()
_highlighter_img = create_highlighter_icon()
_text_img = create_text_icon()
_image_fit_img = create_image_fit_icon()
_image_real_size_img = create_image_real_size_icon()

