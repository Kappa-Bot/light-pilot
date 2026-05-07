using LightPilot.Core;

namespace LightPilot.Core.Tests;

public sealed class ComfortPreferenceAdvisorTests
{
    [Fact]
    public void TooBrightIncreasesComfortGently()
    {
        var settings = UserSettings.Default with { ComfortIntensity = 45 };

        var updated = ComfortPreferenceAdvisor.Apply(settings, ComfortFeedback.TooBright);

        Assert.Equal(50, updated.ComfortIntensity);
        Assert.True(updated.AutoEnabled);
    }

    [Fact]
    public void TooDimDecreasesComfortGently()
    {
        var settings = UserSettings.Default with { ComfortIntensity = 45 };

        var updated = ComfortPreferenceAdvisor.Apply(settings, ComfortFeedback.TooDim);

        Assert.Equal(40, updated.ComfortIntensity);
    }

    [Fact]
    public void FeedbackStaysInsideSafeRange()
    {
        var low = UserSettings.Default with { ComfortIntensity = 0 };
        var high = UserSettings.Default with { ComfortIntensity = 100 };

        Assert.Equal(0, ComfortPreferenceAdvisor.Apply(low, ComfortFeedback.TooDim).ComfortIntensity);
        Assert.Equal(100, ComfortPreferenceAdvisor.Apply(high, ComfortFeedback.TooBright).ComfortIntensity);
    }
}
