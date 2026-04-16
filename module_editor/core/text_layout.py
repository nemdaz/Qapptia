from functools import lru_cache

from PIL import ImageFont
from PySide6.QtCore import QRectF
from PySide6.QtGui import QColor, QFont, QFontDatabase, QTextCharFormat, QTextCursor, QTextDocument, QTextOption

from core import assets
from module_editor import constants


@lru_cache(maxsize=1)
def get_text_font_family():
    font_path = assets.get_text_font_path(constants.TEXT_STYLE["font_files"]["regular"])
    font_id = QFontDatabase.addApplicationFont(font_path)
    if font_id == -1:
        return constants.TEXT_STYLE["fallback_family"]

    families = QFontDatabase.applicationFontFamilies(font_id)
    if not families:
        return constants.TEXT_STYLE["fallback_family"]
    return families[0]


def build_qt_font(pixel_size, bold=False):
    font = QFont(get_text_font_family())
    font.setPixelSize(max(1, int(pixel_size)))
    font.setBold(bold)
    return font


@lru_cache(maxsize=128)
def load_pil_font(pixel_size, bold=False):
    weight_key = "bold" if bold else "regular"
    font_path = assets.get_text_font_path(constants.TEXT_STYLE["font_files"][weight_key])
    return ImageFont.truetype(font_path, max(1, int(pixel_size)))


def get_content_rect(coords, padding):
    x1, y1, x2, y2 = coords
    rect = QRectF(min(x1, x2), min(y1, y2), abs(x2 - x1), abs(y2 - y1))
    if not padding:
        return rect
    return rect.adjusted(padding, padding, -padding, -padding)


def get_text_padding(coords):
    return 0


def fit_text_qt(text, coords, bold=False):
    normalized_text = normalize_text(text)
    padding = get_text_padding(coords)
    rect = get_content_rect(coords, padding)
    if rect.width() <= 0 or rect.height() <= 0:
        font = build_qt_font(constants.TEXT_STYLE["font_min_px"], bold=bold)
        return font, [normalized_text], rect

    pixel_size = _find_best_qt_font_size(normalized_text, rect, bold)
    font = build_qt_font(pixel_size, bold=bold)
    document = create_qt_text_document(normalized_text, font, rect.width())
    return font, document.toPlainText().split("\n"), rect


def fit_text_size_to_fit(text, coords, bold=False):
    normalized_text = normalize_text(text)
    padding = get_text_padding(coords)
    rect = get_content_rect(coords, padding)
    if rect.width() <= 0 or rect.height() <= 0:
        return constants.TEXT_STYLE["font_min_px"]
    return _find_best_qt_font_size(normalized_text, rect, bold)


def create_qt_text_document(text, font, width, color=None):
    document = QTextDocument()
    document.setDocumentMargin(0)
    text_option = QTextOption()
    text_option.setWrapMode(QTextOption.WordWrap)
    document.setDefaultTextOption(text_option)
    document.setDefaultFont(font)
    document.setPlainText(text)
    document.setTextWidth(max(1.0, width))
    if color is not None:
        cursor = QTextCursor(document)
        cursor.select(QTextCursor.Document)
        text_format = QTextCharFormat()
        text_format.setForeground(QColor(color))
        cursor.mergeCharFormat(text_format)
    return document


def fit_text_pil(text, coords, scale=1, bold=False):
    normalized_text = normalize_text(text)
    padding = get_text_padding(coords) * scale
    scaled_coords = [value * scale for value in coords]
    x1, y1, x2, y2 = scaled_coords
    width = abs(x2 - x1)
    height = abs(y2 - y1)
    content_width = max(1, width - (padding * 2))
    content_height = max(1, height - (padding * 2))

    pixel_size = _find_best_pil_font_size(normalized_text, content_width, content_height, bold)
    font = load_pil_font(pixel_size, bold=bold)
    lines = wrap_text_pil(normalized_text, font, content_width)
    return font, lines, padding


def _find_best_qt_font_size(text, rect, bold=False):
    low = constants.TEXT_STYLE["font_min_px"]
    high = _max_font_px(rect)
    best = low

    while low <= high:
        mid = (low + high) // 2
        font = build_qt_font(mid, bold=bold)
        document = create_qt_text_document(text, font, rect.width())
        if document.size().height() <= rect.height():
            best = mid
            low = mid + 1
        else:
            high = mid - 1

    return best


def _find_best_pil_font_size(text, width, height, bold=False):
    low = constants.TEXT_STYLE["font_min_px"]
    high = _max_font_px((width, height))
    best = low

    while low <= high:
        mid = (low + high) // 2
        font = load_pil_font(mid, bold=bold)
        lines = wrap_text_pil(text, font, width)
        total_height = len(lines) * get_pil_line_spacing(font)
        if total_height <= height:
            best = mid
            low = mid + 1
        else:
            high = mid - 1

    return best


def normalize_text(text):
    return (text or constants.TEXT_STYLE["placeholder"]).replace("\r\n", "\n")


def wrap_text_pil(text, font, max_width):
    return _wrap_text(text, max_width, lambda value: font.getbbox(value)[2] - font.getbbox(value)[0])


def get_pil_line_spacing(font):
    bbox = font.getbbox("Ag")
    height = bbox[3] - bbox[1]
    return max(1, int(round(height * constants.TEXT_STYLE["line_spacing_ratio"])))


def _wrap_text(text, max_width, measure):
    if max_width <= 0:
        return [text]

    lines = []
    for paragraph in text.split("\n"):
        if not paragraph:
            lines.append("")
            continue

        current = ""
        for word in paragraph.split(" "):
            candidate = word if not current else f"{current} {word}"
            if measure(candidate) <= max_width:
                current = candidate
                continue

            if current:
                lines.append(current)
            current = _break_long_word(word, max_width, measure, lines)

        if current or not lines:
            lines.append(current)
    return lines or [text]


def _break_long_word(word, max_width, measure, lines):
    if not word:
        return ""
    if measure(word) <= max_width:
        return word

    chunk = ""
    for char in word:
        candidate = f"{chunk}{char}"
        if chunk and measure(candidate) > max_width:
            lines.append(chunk)
            chunk = char
        else:
            chunk = candidate
    return chunk


def _max_font_px(rect):
    if isinstance(rect, tuple):
        width, height = rect
    else:
        width, height = rect.width(), rect.height()
    computed = int(max(1, max(width, height)))
    return max(constants.TEXT_STYLE["font_min_px"], min(constants.TEXT_STYLE["font_max_px"], computed))