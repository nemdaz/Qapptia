import ctypes
from ctypes import wintypes
import threading
import atexit

import keyboard
import mouse
import pystray
from PIL import Image, ImageDraw, ImageGrab

from core.input_runtime import reset_global_input_hooks
from core.platform.base import DesktopService, DpiService, InputService, ProcessService, ScreenService, TrayService


class _WindowsInstanceGuard:
    def __init__(self, key):
        self.key = key
        self._mutex_handle = None
        self._released = False

    def acquire(self):
        mutex_name = f"Global\\QAScreenshot.{self.key}"
        handle = ctypes.windll.kernel32.CreateMutexW(None, False, mutex_name)
        if not handle:
            return False

        error_code = ctypes.windll.kernel32.GetLastError()
        if error_code == 183:
            ctypes.windll.kernel32.CloseHandle(handle)
            return False

        self._mutex_handle = handle
        return True

    def release(self):
        if self._released or self._mutex_handle is None:
            return
        self._released = True
        ctypes.windll.kernel32.CloseHandle(self._mutex_handle)
        self._mutex_handle = None


class WindowsInputService(InputService):
    def requires_process_restart_after_resume(self):
        return True

    def hook_mouse(self, callback):
        return mouse.hook(callback)

    def unhook_all_mouse(self):
        mouse.unhook_all()

    def get_mouse_position(self):
        return mouse.get_position()

    def is_mouse_button_event(self, event):
        return isinstance(event, mouse.ButtonEvent)

    def is_mouse_wheel_event(self, event):
        return isinstance(event, mouse.WheelEvent)

    def is_mouse_move_event(self, event):
        return isinstance(event, mouse.MoveEvent)

    def is_key_pressed(self, key):
        return keyboard.is_pressed(key)

    def add_hotkey(self, hotkey, callback, suppress=False):
        return keyboard.add_hotkey(hotkey, callback, suppress=suppress)

    def remove_hotkey(self, hotkey_handle):
        keyboard.remove_hotkey(hotkey_handle)

    def unhook_all_hotkeys(self):
        keyboard.unhook_all_hotkeys()

    def on_press_key(self, key, callback, suppress=False):
        return keyboard.on_press_key(key, callback, suppress=suppress)

    def unhook_key_listener(self, hook):
        keyboard.unhook(hook)

    def restore_global_hooks_after_resume(self, register_hotkeys_callback, mouse_callback, max_attempts=2, retry_delay_seconds=0.25):
        reset_global_input_hooks(self)
        return False


class WindowsDpiService(DpiService):
    def set_process_dpi_awareness(self):
        try:
            ctypes.windll.shcore.SetProcessDpiAwareness(2)
            return
        except Exception:
            pass

        try:
            ctypes.windll.user32.SetProcessDPIAware()
        except Exception:
            pass


class WindowsProcessService(ProcessService):
    def acquire_single_instance(self, key):
        guard = _WindowsInstanceGuard(key)
        if not guard.acquire():
            return None
        atexit.register(guard.release)
        return guard


class WindowsScreenService(ScreenService):
    def capture_all_screens(self):
        return ImageGrab.grab(all_screens=True)


class WindowsDesktopService(DesktopService):
    def show_info_message(self, title, message):
        ctypes.windll.user32.MessageBoxW(None, message, title, 0x00000040)

    def get_dpi_scaling(self):
        try:
            user32 = ctypes.windll.user32
            gdi32 = ctypes.windll.gdi32
            logical_width = user32.GetSystemMetrics(0)
            hdc = user32.GetDC(0)
            physical_width = gdi32.GetDeviceCaps(hdc, 118)
            user32.ReleaseDC(0, hdc)
            return physical_width / logical_width if logical_width > 0 else 1.0
        except Exception:
            return 1.0

    def get_monitor_at_cursor(self):
        user32 = ctypes.windll.user32
        pt = wintypes.POINT()
        user32.GetCursorPos(ctypes.byref(pt))
        h_monitor = user32.MonitorFromPoint(pt, 1)

        class MONITORINFO(ctypes.Structure):
            _fields_ = [
                ("cbSize", wintypes.DWORD),
                ("rcMonitor", wintypes.RECT),
                ("rcWork", wintypes.RECT),
                ("dwFlags", wintypes.DWORD),
            ]

        mi = MONITORINFO()
        mi.cbSize = ctypes.sizeof(MONITORINFO)
        user32.GetMonitorInfoW(h_monitor, ctypes.byref(mi))
        r = mi.rcMonitor
        return r.left, r.top, r.right - r.left, r.bottom - r.top

    def get_virtual_screen_origin(self):
        user32 = ctypes.windll.user32
        return user32.GetSystemMetrics(76), user32.GetSystemMetrics(77)

    def get_current_cursor(self, scale):
        cursor_img, hotspot = self._get_real_cursor()
        if cursor_img is not None:
            return cursor_img, hotspot
        return self._get_fallback_cursor(scale)

    def _has_visible_cursor_pixels(self, image):
        alpha = image.getchannel("A")
        return alpha.getbbox() is not None

    def _get_fallback_cursor(self, scale):
        base_points = [(0, 0), (0, 16), (4, 12), (7, 19), (9, 18), (6, 11), (11, 11)]
        ss = 6
        canvas_scale = scale * ss
        w, h = int(16 * canvas_scale), int(24 * canvas_scale)
        temp_img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        draw = ImageDraw.Draw(temp_img)

        scaled_points = [(p[0] * canvas_scale, p[1] * canvas_scale) for p in base_points]
        draw.polygon(
            scaled_points,
            fill=(255, 255, 255, 255),
            outline=(0, 0, 0, 255),
            width=max(2, int(canvas_scale * 0.22)),
        )

        final_w, final_h = int(w / ss), int(h / ss)
        return temp_img.resize((final_w, final_h), Image.LANCZOS), (0, 0)

    def _get_real_cursor(self):
        class POINT(ctypes.Structure):
            _fields_ = [("x", wintypes.LONG), ("y", wintypes.LONG)]

        class CURSORINFO(ctypes.Structure):
            _fields_ = [
                ("cbSize", wintypes.DWORD),
                ("flags", wintypes.DWORD),
                ("hCursor", wintypes.HANDLE),
                ("ptScreenPos", POINT),
            ]

        class ICONINFO(ctypes.Structure):
            _fields_ = [
                ("fIcon", wintypes.BOOL),
                ("xHotspot", wintypes.DWORD),
                ("yHotspot", wintypes.DWORD),
                ("hbmMask", wintypes.HANDLE),
                ("hbmColor", wintypes.HANDLE),
            ]

        class BITMAPINFOHEADER(ctypes.Structure):
            _fields_ = [
                ("biSize", wintypes.DWORD),
                ("biWidth", wintypes.LONG),
                ("biHeight", wintypes.LONG),
                ("biPlanes", wintypes.WORD),
                ("biBitCount", wintypes.WORD),
                ("biCompression", wintypes.DWORD),
                ("biSizeImage", wintypes.DWORD),
                ("biXPelsPerMeter", wintypes.LONG),
                ("biYPelsPerMeter", wintypes.LONG),
                ("biClrUsed", wintypes.DWORD),
                ("biClrImportant", wintypes.DWORD),
            ]

        class BITMAPINFO(ctypes.Structure):
            _fields_ = [("bmiHeader", BITMAPINFOHEADER), ("bmiColors", wintypes.DWORD * 3)]

        try:
            u32 = ctypes.windll.user32
            g32 = ctypes.windll.gdi32

            u32.GetCursorInfo.argtypes = [ctypes.POINTER(CURSORINFO)]
            u32.GetCursorInfo.restype = wintypes.BOOL
            u32.GetIconInfo.argtypes = [wintypes.HANDLE, ctypes.POINTER(ICONINFO)]
            u32.GetIconInfo.restype = wintypes.BOOL
            u32.GetDC.argtypes = [wintypes.HWND]
            u32.GetDC.restype = wintypes.HDC
            u32.ReleaseDC.argtypes = [wintypes.HWND, wintypes.HDC]
            u32.ReleaseDC.restype = ctypes.c_int

            g32.CreateCompatibleDC.argtypes = [wintypes.HDC]
            g32.CreateCompatibleDC.restype = wintypes.HDC
            g32.DeleteDC.argtypes = [wintypes.HDC]
            g32.DeleteDC.restype = wintypes.BOOL
            g32.DeleteObject.argtypes = [wintypes.HANDLE]
            g32.DeleteObject.restype = wintypes.BOOL
            g32.CreateCompatibleBitmap.argtypes = [wintypes.HDC, ctypes.c_int, ctypes.c_int]
            g32.CreateCompatibleBitmap.restype = wintypes.HBITMAP
            g32.SelectObject.argtypes = [wintypes.HDC, wintypes.HANDLE]
            g32.SelectObject.restype = wintypes.HANDLE
            u32.DrawIconEx.argtypes = [
                wintypes.HDC,
                ctypes.c_int,
                ctypes.c_int,
                wintypes.HANDLE,
                ctypes.c_int,
                ctypes.c_int,
                ctypes.c_uint,
                wintypes.HBRUSH,
                ctypes.c_uint,
            ]
            u32.DrawIconEx.restype = wintypes.BOOL
            g32.GetDIBits.argtypes = [
                wintypes.HDC,
                wintypes.HBITMAP,
                ctypes.c_uint,
                ctypes.c_uint,
                wintypes.LPVOID,
                ctypes.POINTER(BITMAPINFO),
                ctypes.c_uint,
            ]
            g32.GetDIBits.restype = ctypes.c_int

            ci = CURSORINFO()
            ci.cbSize = ctypes.sizeof(CURSORINFO)
            if not u32.GetCursorInfo(ctypes.byref(ci)) or ci.flags != 1:
                return None, (0, 0)

            ii = ICONINFO()
            if not u32.GetIconInfo(ci.hCursor, ctypes.byref(ii)):
                return None, (0, 0)

            hotspot = (ii.xHotspot, ii.yHotspot)
            cw = u32.GetSystemMetrics(13) or 32
            ch = u32.GetSystemMetrics(14) or 32
            hscr = u32.GetDC(0)
            hmem = g32.CreateCompatibleDC(hscr)
            hbmp = g32.CreateCompatibleBitmap(hscr, cw, ch)
            old_bmp = g32.SelectObject(hmem, hbmp)

            u32.DrawIconEx(hmem, 0, 0, ci.hCursor, cw, ch, 0, 0, 0x0003)
            bmi = BITMAPINFO()
            bmi.bmiHeader.biSize = ctypes.sizeof(BITMAPINFOHEADER)
            bmi.bmiHeader.biWidth = cw
            bmi.bmiHeader.biHeight = -ch
            bmi.bmiHeader.biPlanes = 1
            bmi.bmiHeader.biBitCount = 32
            bmi.bmiHeader.biCompression = 0

            buffer = ctypes.create_string_buffer(cw * ch * 4)
            g32.GetDIBits(hmem, hbmp, 0, ch, buffer, ctypes.byref(bmi), 0)

            if ii.hbmColor:
                g32.DeleteObject(ii.hbmColor)
            if ii.hbmMask:
                g32.DeleteObject(ii.hbmMask)
            g32.SelectObject(hmem, old_bmp)
            g32.DeleteObject(hbmp)
            g32.DeleteDC(hmem)
            u32.ReleaseDC(0, hscr)

            img = Image.frombuffer("RGBA", (cw, ch), buffer, "raw", "BGRA", 0, 1).copy()
            pixels = img.load()
            for y in range(ch):
                for x in range(cw):
                    r, g, b, a = pixels[x, y]
                    if a == 0:
                        pixels[x, y] = (0, 0, 0, 0)
                        continue
                    if a < 255:
                        r = min(255, int((r * 255) / a))
                        g = min(255, int((g * 255) / a))
                        b = min(255, int((b * 255) / a))
                    pixels[x, y] = (r, g, b, a)
            if not self._has_visible_cursor_pixels(img):
                return None, (0, 0)
            return img, hotspot
        except Exception:
            return None, (0, 0)


class WindowsTrayService(TrayService):
    def menu_item(self, title_or_callable, callback, default=False, visible=True):
        return pystray.MenuItem(title_or_callable, callback, default=default, visible=visible)

    def menu_separator(self):
        return pystray.Menu.SEPARATOR

    def menu(self, *items):
        return pystray.Menu(*items)

    def icon(self, name, image, title, menu):
        return pystray.Icon(name, image, title, menu)
