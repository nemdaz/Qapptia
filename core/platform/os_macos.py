from core.platform.unix_common import (
    UnixDesktopService,
    UnixDpiService,
    UnixInputService,
    UnixProcessService,
    UnixScreenService,
    UnixTrayService,
)


class MacOsInputService(UnixInputService):
    pass


class MacOsDpiService(UnixDpiService):
    pass


class MacOsProcessService(UnixProcessService):
    pass


class MacOsScreenService(UnixScreenService):
    pass


class MacOsDesktopService(UnixDesktopService):
    pass


class MacOsTrayService(UnixTrayService):
    pass
