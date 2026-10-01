using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Qapptia.Core;
using Qapptia.Core.Ipc;
using Qapptia.Core.Launcher;
using Qapptia.Core.Platform;
using Xunit;

namespace Qapptia.Core.Tests;

public class LauncherTests
{
    private static readonly string[] s_imageWithSpacesArgs = [@"C:\My Photos\image.png"];

    [Theory]
    [InlineData(null, LaunchTarget.Suite, true, true, false)]
    [InlineData("", LaunchTarget.Suite, true, true, false)]
    [InlineData(Constants.ArgCapture, LaunchTarget.CaptureOnly, true, false, false)]
    [InlineData(Constants.ArgEditor, LaunchTarget.EditorOnly, false, true, false)]
    [InlineData(Constants.ArgConfig, LaunchTarget.ConfigOnly, false, false, true)]
    public void ResolveLaunchPlanInterpretsTargetCorrectly(
        string? arg,
        LaunchTarget expectedTarget,
        bool expectedCapture,
        bool expectedEditor,
        bool expectedConfig)
    {
        string[]? args = string.IsNullOrEmpty(arg) ? (arg == null ? null : Array.Empty<string>()) : [arg];
        var plan = LauncherOrchestrator.ResolveLaunchPlan(args);

        Assert.Equal(expectedTarget, plan.Target);
        Assert.Equal(expectedCapture, plan.ShouldEnsureCapture);
        Assert.Equal(expectedEditor, plan.ShouldEnsureEditor);
        Assert.Equal(expectedConfig, plan.ShouldEnsureConfig);
    }

    [Fact]
    public void ResolveLaunchPlanWithImagePathQuotesArgumentsContainingSpaces()
    {
        var plan = LauncherOrchestrator.ResolveLaunchPlan(s_imageWithSpacesArgs);

        Assert.Equal(LaunchTarget.Suite, plan.Target);
        Assert.True(plan.ShouldEnsureCapture);
        Assert.True(plan.ShouldEnsureEditor);
        Assert.Equal("\"C:\\My Photos\\image.png\"", plan.AdditionalArguments);
    }

    [Fact]
    public async Task DispatchPlanAsyncWhenNeitherRunningStartsBothCaptureAndEditor()
    {
        var mock = new FakeProcessLauncher();
        var plan = new LaunchPlan(LaunchTarget.Suite, null, ShouldEnsureCapture: true, ShouldEnsureEditor: true, ShouldEnsureConfig: false);

        await LauncherOrchestrator.DispatchPlanAsync(plan, @"C:\Qapptia", mock);

        Assert.Equal(2, mock.StartedProcesses.Count);
        Assert.Contains(mock.StartedProcesses, p => p.Executable.Contains("Capture", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(mock.StartedProcesses, p => p.Executable.Contains("Editor", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(mock.WokenChannels);
    }

    [Fact]
    public async Task DispatchPlanAsyncWhenEditorAlreadyRunningSendsWakeUpAndStartsOnlyCapture()
    {
        var mock = new FakeProcessLauncher();
        mock.ActiveChannels.Add(IpcChannels.Editor);
        var plan = new LaunchPlan(LaunchTarget.Suite, null, ShouldEnsureCapture: true, ShouldEnsureEditor: true, ShouldEnsureConfig: false);

        await LauncherOrchestrator.DispatchPlanAsync(plan, @"C:\Qapptia", mock);

        Assert.Single(mock.StartedProcesses);
        Assert.Contains(mock.StartedProcesses, p => p.Executable.Contains("Capture", StringComparison.OrdinalIgnoreCase));
        Assert.Single(mock.WokenChannels);
        Assert.Contains(IpcChannels.Editor, mock.WokenChannels);
    }

    [Fact]
    public async Task DispatchPlanAsyncWhenBothRunningOnlyWakesEditor()
    {
        var mock = new FakeProcessLauncher();
        mock.ActiveChannels.Add(IpcChannels.Capture);
        mock.ActiveChannels.Add(IpcChannels.Editor);
        var plan = new LaunchPlan(LaunchTarget.Suite, null, ShouldEnsureCapture: true, ShouldEnsureEditor: true, ShouldEnsureConfig: false);

        await LauncherOrchestrator.DispatchPlanAsync(plan, @"C:\Qapptia", mock);

        Assert.Empty(mock.StartedProcesses);
        Assert.Single(mock.WokenChannels);
        Assert.Contains(IpcChannels.Editor, mock.WokenChannels);
    }

    [Fact]
    public async Task DispatchPlanAsyncWhenConfigRequestedStartsOrWakesConfig()
    {
        var mock = new FakeProcessLauncher();
        var plan = new LaunchPlan(LaunchTarget.ConfigOnly, null, ShouldEnsureCapture: false, ShouldEnsureEditor: false, ShouldEnsureConfig: true);

        await LauncherOrchestrator.DispatchPlanAsync(plan, @"C:\Qapptia", mock);

        Assert.Single(mock.StartedProcesses);
        Assert.Contains(mock.StartedProcesses, p => p.Executable.Contains("Config", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(mock.WokenChannels);
    }

    [Fact]
    public void MutexSingleInstanceGuardIsRunningReflectsHeldStatus()
    {
        string uniqueKey = "qapptia_unit_test_" + Guid.NewGuid().ToString("N");

        Assert.False(MutexSingleInstanceGuard.IsRunning(uniqueKey));

        using (var guard = new MutexSingleInstanceGuard(uniqueKey))
        {
            var acquired = guard.Acquire();
            Assert.True(acquired);

            Assert.True(MutexSingleInstanceGuard.IsRunning(uniqueKey));

            guard.Release();
        }

        Assert.False(MutexSingleInstanceGuard.IsRunning(uniqueKey));
    }

    [Fact]
    public void ResolveLauncherPathReturnsValidPath()
    {
        string path = LauncherOrchestrator.ResolveLauncherPath();
        Assert.False(string.IsNullOrWhiteSpace(path));
        string expectedExe = OperatingSystem.IsWindows() ? Constants.LauncherExecutableName : Constants.LauncherAppName;
        Assert.EndsWith(expectedExe, path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NullAutoStartServiceBehavesCorrectly()
    {
        var service = Qapptia.Core.Services.NullAutoStartService.Instance;
        Assert.False(service.IsSupported);
        Assert.False(service.IsAutoStartEnabled());
        Assert.False(service.SetAutoStartEnabled(true));
        Assert.False(service.SetAutoStartEnabled(false));
    }

    [Fact]
    public void QapptiaConfigAutoStartSerializationRoundtrip()
    {
        var config = new Qapptia.Core.Configuration.QapptiaConfig { AutoStart = true };
        string json = System.Text.Json.JsonSerializer.Serialize(config);
        Assert.Contains("\"auto_start\":true", json);

        var deserialized = System.Text.Json.JsonSerializer.Deserialize<Qapptia.Core.Configuration.QapptiaConfig>(json);
        Assert.NotNull(deserialized);
        Assert.True(deserialized.AutoStart);
    }

    private sealed class FakeProcessLauncher : IProcessLauncher
    {
        public HashSet<string> ActiveChannels { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<(string Executable, string? Arguments, string WorkingDirectory)> StartedProcesses { get; } = new();
        public List<string> WokenChannels { get; } = new();

        public bool IsInstanceActive(string channel) => ActiveChannels.Contains(channel);

        public bool StartProcess(string executablePath, string? arguments, string workingDirectory)
        {
            StartedProcesses.Add((executablePath, arguments, workingDirectory));
            return true;
        }

        public Task<bool> SendWakeUpAsync(string channel, int timeoutMs = 1000)
        {
            WokenChannels.Add(channel);
            return Task.FromResult(true);
        }
    }
}
