from core.platform.unix_common import (
    UnixDesktopService,
    UnixDpiService,
    UnixInputService,
    UnixProcessService,
    UnixScreenService,
    UnixTrayService,
)


class LinuxInputService(UnixInputService):
    pass


class LinuxDpiService(UnixDpiService):
    pass


class LinuxProcessService(UnixProcessService):
    pass


class LinuxScreenService(UnixScreenService):
    pass


class LinuxDesktopService(UnixDesktopService):
    pass


class LinuxTrayService(UnixTrayService):
    pass
