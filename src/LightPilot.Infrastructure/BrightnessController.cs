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
    private readonly Dictionary<string, DdcFailureState> _ddcFailures = new(StringComparer.OrdinalIgnoreCase);

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

        if (settings.EnableDdcCi && monitor.SupportsBrightnessControl && CanTryDdc(monitor.Id, now))
        {
            if (await _ddcCiApi.TrySetBrightnessAsync(monitor, brightness, cancellationToken).ConfigureAwait(false))
            {
                _ddcFailures.Remove(monitor.Id);
                await _overlayController.ApplyAsync(monitor, decision.OverlayOpacity, decision.TargetColorTemperatureKelvin, cancellationToken).ConfigureAwait(false);
                _lastWrites[monitor.Id] = now;
                return;
            }

            RecordDdcFailure(monitor.Id, now);
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

    private bool CanTryDdc(string monitorId, DateTimeOffset now)
    {
        return !_ddcFailures.TryGetValue(monitorId, out var state) || state.SuppressedUntil <= now;
    }

    private void RecordDdcFailure(string monitorId, DateTimeOffset now)
    {
        var count = _ddcFailures.TryGetValue(monitorId, out var current) ? current.Count + 1 : 1;
        var suppressedUntil = count >= 3 ? now.AddMinutes(10) : now;
        _ddcFailures[monitorId] = new DdcFailureState(count, suppressedUntil);
    }

    private sealed record DdcFailureState(int Count, DateTimeOffset SuppressedUntil);
}
