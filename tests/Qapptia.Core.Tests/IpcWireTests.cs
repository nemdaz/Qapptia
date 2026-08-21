using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Qapptia.Core.Ipc;
using Serilog;
using Xunit;

namespace Qapptia.Core.Tests;

public class IpcWireTests
{
    [Fact]
    public async Task ThemeChangedNotificationShouldEncodeAndDecodeCorrectly()
    {
        var original = new ThemeChangedNotification { Theme = "light" };
        using var stream = new MemoryStream();

        await IpcWire.WriteFrameAsync(stream, original);
        stream.Position = 0;

        var decoded = await IpcWire.ReadFrameAsync(stream);

        Assert.NotNull(decoded);
        var themeMsg = Assert.IsType<ThemeChangedNotification>(decoded);
        Assert.Equal("light", themeMsg.Theme);
        Assert.Equal(IpcMessageType.ThemeChanged, themeMsg.Type);
    }

    [Fact]
    public async Task RefreshTrayIconRequestShouldEncodeAndDecodeCorrectly()
    {
        var original = new RefreshTrayIconRequest();
        using var stream = new MemoryStream();

        await IpcWire.WriteFrameAsync(stream, original);
        stream.Position = 0;

        var decoded = await IpcWire.ReadFrameAsync(stream);

        Assert.NotNull(decoded);
        Assert.IsType<RefreshTrayIconRequest>(decoded);
        Assert.Equal(IpcMessageType.RefreshTrayIcon, decoded.Type);
    }

    [Fact]
    public async Task AllIpcMessageTypesShouldRoundtripSuccessfully()
    {
        IpcMessage[] messages = [
            new WakeUpRequest(),
            new QuitRequest(),
            new RefreshTrayIconRequest(),
            new Ping(),
            new Pong { ServerPid = 1234 },
            new Ack { OriginalType = IpcMessageType.ThemeChanged },
            new ErrorResponse { Reason = "Test reason" },
            new ThemeChangedNotification { Theme = "dark" }
        ];

        foreach (var msg in messages)
        {
            using var stream = new MemoryStream();
            await IpcWire.WriteFrameAsync(stream, msg);
            stream.Position = 0;

            var decoded = await IpcWire.ReadFrameAsync(stream);
            Assert.NotNull(decoded);
            Assert.Equal(msg.Type, decoded.Type);
            Assert.Equal(msg.GetType(), decoded.GetType());
        }
    }

    [Fact]
    public async Task QapptiaIpcClientAndServerShouldCommunicateEndToEnd()
    {
        var testChannel = "test_channel_" + Guid.NewGuid().ToString("N")[..8];
        var testPipe = testChannel;
        var logger = new LoggerConfiguration().CreateLogger();

        string? receivedTheme = null;
        var dispatcher = new IpcMessageDispatcher(
            (msg, ct) =>
            {
                if (msg is ThemeChangedNotification themeMsg)
                {
                    receivedTheme = themeMsg.Theme;
                    return Task.FromResult<IpcMessage>(new Ack { OriginalType = msg.Type });
                }
                return Task.FromResult<IpcMessage>(new Ack { OriginalType = msg.Type });
            },
            logger);

        using var server = new QapptiaIpcServer(testChannel, testPipe, dispatcher, logger);
        await server.StartAsync();

        try
        {
            var response = await QapptiaIpcClient.SendAsync(testChannel, new ThemeChangedNotification { Theme = "light" });
            Assert.NotNull(response);
            var ack = Assert.IsType<Ack>(response);
            Assert.Equal(IpcMessageType.ThemeChanged, ack.OriginalType);
            Assert.Equal("light", receivedTheme);
        }
        finally
        {
            await server.StopAsync();
        }
    }
}
