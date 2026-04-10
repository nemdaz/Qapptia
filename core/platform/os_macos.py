from core.platform.base import DesktopService, DpiService, InputService, ProcessService, ScreenService, TrayService
from core.platform.unix_helper import (
    UnixDesktopMixin,
    UnixDpiMixin,
    UnixInputMixin,
    UnixProcessMixin,
    UnixScreenMixin,
    UnixTrayMixin,
)


class MacOsInputService(UnixInputMixin, InputService):
    pass


class MacOsDpiService(UnixDpiMixin, DpiService):
    pass


class MacOsProcessService(UnixProcessMixin, ProcessService):
    pass


class MacOsScreenService(UnixScreenMixin, ScreenService):
    pass


class MacOsDesktopService(UnixDesktopMixin, DesktopService):
    pass


class MacOsTrayService(UnixTrayMixin, TrayService):
    pass
