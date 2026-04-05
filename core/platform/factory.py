import sys
from dataclasses import dataclass

from core.platform.base import DpiService, InputService, ScreenService
from core.platform.posix import PosixDpiService, PosixInputService, PosixScreenService
from core.platform.windows import WindowsDpiService, WindowsInputService, WindowsScreenService


@dataclass(frozen=True)
class PlatformServices:
    input: InputService
    dpi: DpiService
    screen: ScreenService


_services_singleton = None


def get_platform_services():
    global _services_singleton
    if _services_singleton is not None:
        return _services_singleton

    if sys.platform == "win32":
        _services_singleton = PlatformServices(
            input=WindowsInputService(),
            dpi=WindowsDpiService(),
            screen=WindowsScreenService(),
        )
        return _services_singleton

    _services_singleton = PlatformServices(
        input=PosixInputService(),
        dpi=PosixDpiService(),
        screen=PosixScreenService(),
    )
    return _services_singleton
