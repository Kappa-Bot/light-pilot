using LightPilot.Core;
using LightPilot.Infrastructure;

namespace LightPilot.Infrastructure.Tests;

public sealed class BrightnessControllerTests
{
    [Fact]
    public async Task UsesDdcCiWhenSupportedAndEnabled()
    {
        var ddc = new FakeDdcCiApi(canSet: true);
        var windows = new FakeWindowsBrightnessApi(canSet: true);
        var overlay = new FakeOverlayController();
        var controller = new BrightnessController(ddc, windows, overlay, TimeProvider.System);
        var monitor = Monitor with { SupportsBrightnessControl = true };

        await controller.ApplyAsync(monitor, Decision(52), UserSettings.Default, CancellationToken.None);

        Assert.Equal(52, ddc.LastBrightness);
        Assert.Null(windows.LastBrightness);
        Assert.Equal(0, overlay.LastOpacity);
    }

    [Fact]
    public async Task AppliesWarmOverlayWhenHardwareBrightnessSucceeds()
    {
        var ddc = new FakeDdcCiApi(canSet: true);
        var overlay = new FakeOverlayController();
        var controller = new BrightnessController(ddc, new FakeWindowsBrightnessApi(canSet: false), overlay, TimeProvider.System);
        var monitor = Monitor with { SupportsBrightnessControl = true };

        await controller.ApplyAsync(monitor, Decision(52, overlayOpacity: 0.08), UserSettings.Default, CancellationToken.None);

        Assert.Equal(52, ddc.LastBrightness);
        Assert.Equal(0.08, overlay.LastOpacity);
    }

    [Fact]
    public async Task FallsBackToWindowsBrightnessThenOverlay()
    {
        var ddc = new FakeDdcCiApi(canSet: false);
        var windows = new FakeWindowsBrightnessApi(canSet: false);
        var overlay = new FakeOverlayController();
        var controller = new BrightnessController(ddc, windows, overlay, TimeProvider.System);

        await controller.ApplyAsync(Monitor, Decision(45, overlayOpacity: 0.2), UserSettings.Default, CancellationToken.None);

        Assert.Equal(0.2, overlay.LastOpacity);
    }

    [Fact]
    public async Task ThrottlesWritesWithinTwoSecondsPerMonitor()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero));
        var ddc = new FakeDdcCiApi(canSet: true);
        var controller = new BrightnessController(ddc, new FakeWindowsBrightnessApi(canSet: true), new FakeOverlayController(), time);

        await controller.ApplyAsync(Monitor, Decision(50), UserSettings.Default, CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(1));
        await controller.ApplyAsync(Monitor, Decision(55), UserSettings.Default, CancellationToken.None);

        Assert.Equal(1, ddc.WriteCount);
        Assert.Equal(50, ddc.LastBrightness);
    }

    [Fact]
    public async Task BacksOffDdcAfterRepeatedFailures()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero));
        var ddc = new FakeDdcCiApi(canSet: false);
        var windows = new FakeWindowsBrightnessApi(canSet: true);
        var controller = new BrightnessController(ddc, windows, new FakeOverlayController(), time);
        var monitor = Monitor with { SupportsBrightnessControl = true };

        for (var i = 0; i < 3; i++)
        {
            await controller.ApplyAsync(monitor, Decision(50 + i), UserSettings.Default, CancellationToken.None);
            time.Advance(TimeSpan.FromSeconds(3));
        }

        await controller.ApplyAsync(monitor, Decision(60), UserSettings.Default, CancellationToken.None);

        Assert.Equal(3, ddc.WriteCount);
        Assert.Equal(60, windows.LastBrightness);
    }

    private static MonitorModel Monitor => new("m1", "Desk", true, true, 15, 100, 0);

    private static ComfortDecision Decision(int brightness, double overlayOpacity = 0)
    {
        return new ComfortDecision(ComfortProfileId.Evening, brightness, 4200, overlayOpacity, TimeSpan.FromSeconds(45), true, "test", Array.Empty<string>());
    }
}

internal sealed class FakeDdcCiApi(bool canSet) : IDdcCiApi
{
    public int? LastBrightness { get; private set; }
    public int WriteCount { get; private set; }

    public ValueTask<bool> TrySetBrightnessAsync(MonitorModel monitor, int brightnessPercent, CancellationToken cancellationToken)
    {
        WriteCount++;
        if (canSet)
        {
            LastBrightness = brightnessPercent;
        }

        return ValueTask.FromResult(canSet);
    }
}

internal sealed class FakeWindowsBrightnessApi(bool canSet) : IWindowsBrightnessApi
{
    public int? LastBrightness { get; private set; }

    public ValueTask<bool> TrySetBrightnessAsync(MonitorModel monitor, int brightnessPercent, CancellationToken cancellationToken)
    {
        if (canSet)
        {
            LastBrightness = brightnessPercent;
        }

        return ValueTask.FromResult(canSet);
    }
}

internal sealed class FakeOverlayController : IOverlayController
{
    public double? LastOpacity { get; private set; }

    public ValueTask ApplyAsync(MonitorModel monitor, double opacity, int colorTemperatureKelvin, CancellationToken cancellationToken)
    {
        LastOpacity = opacity;
        return ValueTask.CompletedTask;
    }
}

internal sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by)
    {
        _now += by;
    }
}
