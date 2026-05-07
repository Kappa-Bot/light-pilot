using LightPilot.Core;

namespace LightPilot.Core.Tests;

public sealed class UserSettingsTests
{
    [Fact]
    public void DefaultsFavorGentleComfort()
    {
        Assert.Equal(50, UserSettings.Default.ComfortIntensity);
        Assert.Equal(25, UserSettings.Default.MinimumBrightnessPercent);
        Assert.Equal(90, UserSettings.Default.MaximumBrightnessPercent);
    }
}
