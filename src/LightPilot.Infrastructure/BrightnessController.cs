using LightPilot.Core;

namespace LightPilot.Infrastructure;

public sealed class BrightnessController : IBrightnessController
{
    private static readonly TimeSpan MinimumWriteInterval = TimeSpan.FromSeconds(2);

    private readonly IDdcCiApi _ddcCiApi;
    private readonly IWindowsBrightnessApi _windowsBrightnessApi;
    private readonly IOverlayController _overlayController;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, DateTimeOffset> _lastWrites = new(StringComparer.OrdinalIgnoreCase);

    public BrightnessController(IDdcCiApi ddcCiApi, IWindowsBrightnessApi windowsBrightnessApi, IOverlayController overlayController, TimeProvider? timeProvider = null)
    {
        _ddcCiApi = ddcCiApi;
        _windowsBrightnessApi = windowsBrightnessApi;
        _overlayController = overlayController;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async ValueTask ApplyAsync(MonitorModel monitor, ComfortDecision decision, UserSettings settings, CancellationToken cancellationToken)
    {
        if (!decision.ShouldApply)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        if (_lastWrites.TryGetValue(monitor.Id, out var lastWrite) && now - lastWrite < MinimumWriteInterval)
        {
            return;
        }

        var brightness = Math.Clamp(decision.TargetBrightnessPercent, monitor.MinimumBrightnessPercent, monitor.MaximumBrightnessPercent);

        if (settings.EnableDdcCi && monitor.SupportsBrightnessControl)
        {
            if (await _ddcCiApi.TrySetBrightnessAsync(monitor, brightness, cancellationToken).ConfigureAwait(false))
            {
                await _overlayController.ApplyAsync(monitor, decision.OverlayOpacity, decision.TargetColorTemperatureKelvin, cancellationToken).ConfigureAwait(false);
                _lastWrites[monitor.Id] = now;
                return;
            }
        }

        if (await _windowsBrightnessApi.TrySetBrightnessAsync(monitor, brightness, cancellationToken).ConfigureAwait(false))
        {
            await _overlayController.ApplyAsync(monitor, decision.OverlayOpacity, decision.TargetColorTemperatureKelvin, cancellationToken).ConfigureAwait(false);
            _lastWrites[monitor.Id] = now;
            return;
        }

        await _overlayController.ApplyAsync(monitor, decision.OverlayOpacity, decision.TargetColorTemperatureKelvin, cancellationToken).ConfigureAwait(false);
        _lastWrites[monitor.Id] = now;
    }
}
