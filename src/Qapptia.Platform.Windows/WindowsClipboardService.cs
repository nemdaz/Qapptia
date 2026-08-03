using SkiaSharp;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Qapptia.Core.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Qapptia.Platform.Windows;

public sealed class WindowsClipboardService : IClipboardService
{
    private readonly ILogger<WindowsClipboardService> _logger;

    public WindowsClipboardService(ILogger<WindowsClipboardService> logger)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("WindowsClipboardService requiere Windows.");
        _logger = logger;
    }

    public Task SetTextAsync(string text, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task SetImageAsync(byte[] pngBytes, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task SetFileDropListAsync(string[] filePaths, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
