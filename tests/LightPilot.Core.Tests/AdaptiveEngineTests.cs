using LightPilot.Core;

namespace LightPilot.Core.Tests;

public sealed class AdaptiveEngineTests
{
    [Fact]
    public void BrightBrowserContentAtNightReducesBrightnessAndAddsWarmth()
    {
        var engine = new AdaptiveEngine();
        var snapshot = TestSnapshots.Default with
        {
            Now = new DateTimeOffset(2026, 5, 7, 23, 30, 0, TimeSpan.Zero),
            AppContext = new AppContextModel("chrome.exe", AppCategory.Browser, isFullscreen: false),
            Content = new ContentLuminanceSample(true, 0.78, 0.66, 0.71, 0.02, LuminanceClassification.MostlyWhite),
            CurrentBrightness = 70
        };

        var decision = engine.Evaluate(snapshot, AdaptiveEngineState.Empty, UserSettings.Default);

        Assert.Equal(ComfortProfileId.Reading, decision.Profile);
        Assert.Equal(67, decision.TargetBrightnessPercent);
        Assert.True(decision.TargetColorTemperatureKelvin <= 3600);
        Assert.Equal("Bright reading content at night", decision.Reason);
        Assert.True(decision.ShouldApply);
    }

    [Fact]
    public void FullscreenGameProtectsBrightnessAndUsesMildWarmth()
    {
        var engine = new AdaptiveEngine();
        var snapshot = TestSnapshots.Default with
        {
            Now = new DateTimeOffset(2026, 5, 7, 22, 0, 0, TimeSpan.Zero),
            AppContext = new AppContextModel("Overwatch.exe", AppCategory.Gaming, isFullscreen: true),
            CurrentBrightness = 68,
            CurrentColorTemperatureKelvin = 6500
        };

        var decision = engine.Evaluate(snapshot, AdaptiveEngineState.Empty, UserSettings.Default);

        Assert.Equal(ComfortProfileId.Gaming, decision.Profile);
        Assert.Equal(68, decision.TargetBrightnessPercent);
        Assert.InRange(decision.TargetColorTemperatureKelvin, 4300, 5200);
        Assert.Equal("Gaming fullscreen protection", decision.Reason);
    }

    [Fact]
    public void LateDevelopmentSessionAppliesModerateReductionAndMediumWarmth()
    {
        var engine = new AdaptiveEngine();
        var snapshot = TestSnapshots.Default with
        {
            Now = new DateTimeOffset(2026, 5, 8, 1, 30, 0, TimeSpan.Zero),
            AppContext = new AppContextModel("Code.exe", AppCategory.Development, isFullscreen: false),
            Content = new ContentLuminanceSample(true, 0.18, 0.01, 0.04, 0.75, LuminanceClassification.Dark),
            CurrentBrightness = 70
        };

        var decision = engine.Evaluate(snapshot, AdaptiveEngineState.Empty, UserSettings.Default);

        Assert.Equal(ComfortProfileId.Development, decision.Profile);
        Assert.Equal(67, decision.TargetBrightnessPercent);
        Assert.InRange(decision.TargetColorTemperatureKelvin, 3300, 4300);
        Assert.Equal("Late development session", decision.Reason);
    }

    [Fact]
    public void FullscreenVideoUsesMinimalAdjustment()
    {
        var engine = new AdaptiveEngine();
        var snapshot = TestSnapshots.Default with
        {
            Now = new DateTimeOffset(2026, 5, 7, 20, 0, 0, TimeSpan.Zero),
            AppContext = new AppContextModel("vlc.exe", AppCategory.VideoMedia, isFullscreen: true),
            CurrentBrightness = 61
        };

        var decision = engine.Evaluate(snapshot, AdaptiveEngineState.Empty, UserSettings.Default);

        Assert.Equal(ComfortProfileId.Video, decision.Profile);
        Assert.Equal(61, decision.TargetBrightnessPercent);
        Assert.Equal("Video playback protected", decision.Reason);
    }

    [Fact]
    public void SafetyClampWinsAfterAllAdjustments()
    {
        var engine = new AdaptiveEngine();
        var settings = UserSettings.Default with { MinimumBrightnessPercent = 35, MaximumBrightnessPercent = 65, ComfortIntensity = 100 };
        var snapshot = TestSnapshots.Default with
        {
            Now = new DateTimeOffset(2026, 5, 8, 2, 15, 0, TimeSpan.Zero),
            AppContext = new AppContextModel("chrome.exe", AppCategory.Browser, isFullscreen: false),
            Content = new ContentLuminanceSample(true, 0.92, 0.85, 0.9, 0, LuminanceClassification.MostlyWhite),
            CurrentBrightness = 80
        };

        var decision = engine.Evaluate(snapshot, AdaptiveEngineState.Empty, settings);

        Assert.InRange(decision.TargetBrightnessPercent, 35, 65);
    }

    [Fact]
    public void AutomaticBrightnessChangeIsLimitedToThreePointsPerDecision()
    {
        var engine = new AdaptiveEngine();
        var snapshot = TestSnapshots.Default with
        {
            Now = new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero),
            CurrentBrightness = 40
        };

        var decision = engine.Evaluate(snapshot, AdaptiveEngineState.Empty, UserSettings.Default);

        Assert.Equal(43, decision.TargetBrightnessPercent);
    }

    [Fact]
    public void HysteresisSuppressesTinyChanges()
    {
        var engine = new AdaptiveEngine();
        var snapshot = TestSnapshots.Default with
        {
            Now = new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero),
            CurrentBrightness = 80,
            CurrentColorTemperatureKelvin = 6500
        };
        var state = AdaptiveEngineState.Empty with
        {
            LastAppliedAt = snapshot.Now.AddSeconds(-90),
            LastDecision = new LightTarget(79, 6450, 0)
        };

        var decision = engine.Evaluate(snapshot, state, UserSettings.Default);

        Assert.False(decision.ShouldApply);
        Assert.Equal("No visible change needed", decision.Reason);
    }

    [Fact]
    public void CooldownSuppressesFrequentAutomaticChanges()
    {
        var engine = new AdaptiveEngine();
        var snapshot = TestSnapshots.Default with
        {
            Now = new DateTimeOffset(2026, 5, 7, 22, 5, 10, TimeSpan.Zero),
            CurrentBrightness = 80
        };
        var state = AdaptiveEngineState.Empty with
        {
            LastAppliedAt = snapshot.Now.AddSeconds(-10),
            LastDecision = new LightTarget(62, 4100, 0.15)
        };

        var decision = engine.Evaluate(snapshot, state, UserSettings.Default);

        Assert.False(decision.ShouldApply);
        Assert.Equal("Cooling down before next adjustment", decision.Reason);
    }
}

internal static class TestSnapshots
{
    public static AdaptiveSnapshot Default => new(
        Now: new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero),
        Monitor: new MonitorModel("monitor-1", "Primary", true, true, 15, 100, 0),
        AppContext: new AppContextModel("notepad.exe", AppCategory.OfficeReading, isFullscreen: false),
        Content: ContentLuminanceSample.Unknown,
        ScreenTimeSessionLength: TimeSpan.FromMinutes(20),
        CurrentBrightness: 70,
        CurrentColorTemperatureKelvin: 6500,
        ManualOverrideUntil: null);
}
