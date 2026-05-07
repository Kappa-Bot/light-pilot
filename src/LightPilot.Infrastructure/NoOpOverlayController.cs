using LightPilot.Core;

namespace LightPilot.Infrastructure;

public sealed class NoOpOverlayController : IOverlayController
{
    public ValueTask ApplyAsync(MonitorModel monitor, double opacity, int colorTemperatureKelvin, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }
}
