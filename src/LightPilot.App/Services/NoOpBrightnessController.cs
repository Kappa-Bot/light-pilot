using LightPilot.Core;
using LightPilot.Infrastructure;

namespace LightPilot.App.Services;

public sealed class NoOpBrightnessController : IBrightnessController
{
    public ValueTask ApplyAsync(MonitorModel monitor, ComfortDecision decision, UserSettings settings, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }
}
