from core.platform.base import DesktopService, DpiService, InputService, ProcessService, ScreenService, TrayService
from core.platform.unix_helper import (
    UnixDesktopMixin,
    UnixDpiMixin,
    UnixInputMixin,
    UnixProcessMixin,
    UnixScreenMixin,
    UnixTrayMixin,
)


class LinuxInputService(UnixInputMixin, InputService):
    pass


class LinuxDpiService(UnixDpiMixin, DpiService):
    pass


class LinuxProcessService(UnixProcessMixin, ProcessService):
    pass


class LinuxScreenService(UnixScreenMixin, ScreenService):
    pass


class LinuxDesktopService(UnixDesktopMixin, DesktopService):
    pass


class LinuxTrayService(UnixTrayMixin, TrayService):
    pass
