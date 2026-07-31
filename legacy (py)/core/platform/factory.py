import sys
from dataclasses import dataclass

from core.platform.base import DesktopService, DpiService, InputService, ProcessService, ScreenService, TrayService
from core.platform.os_linux import (
    LinuxDesktopService,
    LinuxDpiService,
    LinuxInputService,
    LinuxProcessService,
    LinuxScreenService,
    LinuxTrayService,
)
from core.platform.os_macos import (
    MacOsDesktopService,
    MacOsDpiService,
    MacOsInputService,
    MacOsProcessService,
    MacOsScreenService,
    MacOsTrayService,
)
from core.platform.os_windows import (
    WindowsDesktopService,
    WindowsDpiService,
    WindowsInputService,
    WindowsProcessService,
    WindowsScreenService,
    WindowsTrayService,
)


@dataclass(frozen=True)
class PlatformServices:
    input: InputService
    dpi: DpiService
    process: ProcessService
    screen: ScreenService
    desktop: DesktopService
    tray: TrayService


_services_singleton = None


def get_platform_services():
    global _services_singleton
    if _services_singleton is not None:
        return _services_singleton

    if sys.platform == "win32":
        _services_singleton = PlatformServices(
            input=WindowsInputService(),
            dpi=WindowsDpiService(),
            process=WindowsProcessService(),
            screen=WindowsScreenService(),
            desktop=WindowsDesktopService(),
            tray=WindowsTrayService(),
        )
        return _services_singleton

    if sys.platform == "darwin":
        _services_singleton = PlatformServices(
            input=MacOsInputService(),
            dpi=MacOsDpiService(),
            process=MacOsProcessService(),
            screen=MacOsScreenService(),
            desktop=MacOsDesktopService(),
            tray=MacOsTrayService(),
        )
        return _services_singleton

    if sys.platform.startswith("linux"):
        _services_singleton = PlatformServices(
            input=LinuxInputService(),
            dpi=LinuxDpiService(),
            process=LinuxProcessService(),
            screen=LinuxScreenService(),
            desktop=LinuxDesktopService(),
            tray=LinuxTrayService(),
        )
        return _services_singleton

    raise RuntimeError(f"Unsupported platform for platform services: {sys.platform}")
