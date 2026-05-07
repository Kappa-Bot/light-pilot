namespace LightPilot.Core;

public sealed class AdaptiveEngine
{
    private static readonly TimeSpan AutomaticCooldown = TimeSpan.FromSeconds(30);
    private readonly ProfileManager _profiles = new();

    public ComfortDecision Evaluate(AdaptiveSnapshot snapshot, AdaptiveEngineState state, UserSettings settings)
    {
        if (!settings.AutoEnabled)
        {
            return Decision(ComfortProfileId.Paused, snapshot.CurrentBrightness, snapshot.CurrentColorTemperatureKelvin, 0, false, "Auto is paused", "PolicyDisabled", settings);
        }

        if (snapshot.ManualOverrideUntil is { } manualUntil && manualUntil > snapshot.Now)
        {
            return Decision(ComfortProfileId.Manual, snapshot.CurrentBrightness, snapshot.CurrentColorTemperatureKelvin, 0, false, "Manual adjustment cooldown", "ManualOverrideActive", settings);
        }

        var profile = SelectProfile(snapshot);

        if (settings.GamingVideoProtection && snapshot.AppContext.IsPresentation)
        {
            return Decision(ComfortProfileId.Video, snapshot.CurrentBrightness, snapshot.CurrentColorTemperatureKelvin, 0, false, "Presentation protected", "SuppressedForPresentation", settings);
        }

        if (settings.GamingVideoProtection && snapshot.AppContext.Category == AppCategory.Gaming && snapshot.AppContext.IsFullscreen)
        {
            return Decision(ComfortProfileId.Gaming, snapshot.CurrentBrightness, MildWarmth(snapshot.Now), 0, true, "Gaming fullscreen protection", "SuppressedForGame", settings);
        }

        if (settings.GamingVideoProtection && snapshot.AppContext.Category == AppCategory.VideoMedia && snapshot.AppContext.IsFullscreen)
        {
            return Decision(ComfortProfileId.Video, snapshot.CurrentBrightness, MildWarmth(snapshot.Now), 0, true, "Video playback protected", "SuppressedForVideoPlayback", settings);
        }

        var raw = ComputeTarget(snapshot, profile, settings);
        var targetBrightness = raw.BrightnessPercent;
        var targetKelvin = raw.ColorTemperatureKelvin;
        var reason = BuildReason(snapshot, profile);
        var reasonCode = profile.ToString();

        if (state.LastAppliedAt is { } lastApplied && snapshot.Now - lastApplied < AutomaticCooldown)
        {
            return new ComfortDecision(profile, targetBrightness, targetKelvin, raw.OverlayOpacity, settings.TransitionSpeed, false, "Cooling down before next adjustment", new[] { "CooldownActive" });
        }

        if (state.LastDecision is { } last && Math.Abs(targetBrightness - last.BrightnessPercent) < 2 && Math.Abs(targetKelvin - last.ColorTemperatureKelvin) < 100)
        {
            return new ComfortDecision(profile, targetBrightness, targetKelvin, raw.OverlayOpacity, settings.TransitionSpeed, false, "No visible change needed", new[] { "NoChangeWithinHysteresis" });
        }

        return new ComfortDecision(profile, targetBrightness, targetKelvin, raw.OverlayOpacity, settings.TransitionSpeed, true, reason, new[] { reasonCode });
    }

    private LightTarget ComputeTarget(AdaptiveSnapshot snapshot, ComfortProfileId profileId, UserSettings settings)
    {
        var profile = _profiles.Get(profileId);
        var phase = GetDayPhase(snapshot.Now.TimeOfDay);
        var brightness = phase switch
        {
            DayPhase.Day => profile.DayBrightness,
            DayPhase.Evening => profile.EveningBrightness,
            DayPhase.Night => profile.NightBrightness,
            _ => profile.DayBrightness
        };
        var kelvin = phase switch
        {
            DayPhase.Day => profile.DayKelvin,
            DayPhase.Evening => profile.EveningKelvin,
            DayPhase.Night => profile.NightKelvin,
            _ => profile.DayKelvin
        };

        var intensityFactor = Math.Clamp(settings.ComfortIntensity, 0, 100) / 100d;
        if (snapshot.Content.IsEnabled && snapshot.AppContext.Category is AppCategory.Browser or AppCategory.EmailCommunication or AppCategory.OfficeReading)
        {
            if (snapshot.Content.Classification == LuminanceClassification.MostlyWhite)
            {
                brightness -= (int)Math.Round(6 * intensityFactor);
                kelvin -= (int)Math.Round(220 * intensityFactor);
            }
            else if (snapshot.Content.Classification == LuminanceClassification.Bright)
            {
                brightness -= (int)Math.Round(3 * intensityFactor);
                kelvin -= (int)Math.Round(120 * intensityFactor);
            }
        }

        if (snapshot.ScreenTimeSessionLength >= TimeSpan.FromMinutes(75) && snapshot.AppContext.Category is not (AppCategory.Gaming or AppCategory.VideoMedia))
        {
            var excess = Math.Min(1, (snapshot.ScreenTimeSessionLength - TimeSpan.FromMinutes(75)).TotalMinutes / 45d);
            brightness -= (int)Math.Round(4 * excess * intensityFactor);
            kelvin -= (int)Math.Round(180 * excess * intensityFactor);
        }

        if (snapshot.Now.TimeOfDay < TimeSpan.FromHours(3))
        {
            brightness -= (int)Math.Round(2 * intensityFactor);
            kelvin -= (int)Math.Round(140 * intensityFactor);
        }

        brightness += snapshot.Monitor.BrightnessOffsetPercent;
        brightness = LimitStep(snapshot.CurrentBrightness, brightness, maxStep: 3);
        brightness = ClampBrightness(brightness, snapshot.Monitor, settings);
        kelvin = Math.Clamp(kelvin, 2800, 6500);
        kelvin = LimitStep(snapshot.CurrentColorTemperatureKelvin, kelvin, maxStep: 200);
        var overlay = Math.Clamp((6500 - kelvin) / 3700d * 0.14, 0, 0.24);

        return new LightTarget(brightness, kelvin, overlay);
    }

    private static ComfortProfileId SelectProfile(AdaptiveSnapshot snapshot)
    {
        if (snapshot.AppContext.Category == AppCategory.Gaming)
        {
            return ComfortProfileId.Gaming;
        }

        if (snapshot.AppContext.Category == AppCategory.VideoMedia)
        {
            return ComfortProfileId.Video;
        }

        if (snapshot.AppContext.Category == AppCategory.Development)
        {
            return ComfortProfileId.Development;
        }

        if (snapshot.AppContext.Category is AppCategory.Browser or AppCategory.EmailCommunication or AppCategory.OfficeReading
            && snapshot.Content.Classification is LuminanceClassification.Bright or LuminanceClassification.MostlyWhite
            && GetDayPhase(snapshot.Now.TimeOfDay) != DayPhase.Day)
        {
            return ComfortProfileId.Reading;
        }

        return GetDayPhase(snapshot.Now.TimeOfDay) switch
        {
            DayPhase.Day => ComfortProfileId.Day,
            DayPhase.Evening => ComfortProfileId.Evening,
            _ => ComfortProfileId.Night
        };
    }

    private static string BuildReason(AdaptiveSnapshot snapshot, ComfortProfileId profile)
    {
        if (profile == ComfortProfileId.Reading && snapshot.Content.Classification == LuminanceClassification.MostlyWhite)
        {
            return "Bright reading content at night";
        }

        if (profile == ComfortProfileId.Development && snapshot.Now.TimeOfDay < TimeSpan.FromHours(3))
        {
            return "Late development session";
        }

        return profile switch
        {
            ComfortProfileId.Day => "Daylight comfort",
            ComfortProfileId.Evening => "Evening comfort",
            ComfortProfileId.Night => "Night comfort",
            ComfortProfileId.Development => "Development comfort",
            ComfortProfileId.Reading => "Reading comfort",
            _ => "Auto comfort"
        };
    }

    private static int ClampBrightness(int brightness, MonitorModel monitor, UserSettings settings)
    {
        var lower = Math.Max(15, Math.Max(monitor.MinimumBrightnessPercent, settings.MinimumBrightnessPercent));
        var upper = Math.Min(100, Math.Min(monitor.MaximumBrightnessPercent, settings.MaximumBrightnessPercent));
        if (lower > upper)
        {
            lower = upper;
        }

        return Math.Clamp(brightness, lower, upper);
    }

    private static int LimitStep(int current, int target, int maxStep)
    {
        if (current <= 0)
        {
            return target;
        }

        var delta = target - current;
        if (Math.Abs(delta) <= maxStep)
        {
            return target;
        }

        return current + (Math.Sign(delta) * maxStep);
    }

    private static ComfortDecision Decision(ComfortProfileId profile, int brightness, int kelvin, double opacity, bool shouldApply, string reason, string code, UserSettings settings)
    {
        return new ComfortDecision(profile, brightness, kelvin, opacity, settings.TransitionSpeed, shouldApply, reason, new[] { code });
    }

    private static int MildWarmth(DateTimeOffset now)
    {
        return GetDayPhase(now.TimeOfDay) switch
        {
            DayPhase.Day => 6500,
            DayPhase.Evening => 5000,
            _ => 4700
        };
    }

    private static DayPhase GetDayPhase(TimeSpan time)
    {
        if (time >= TimeSpan.FromHours(8) && time < TimeSpan.FromHours(17.5))
        {
            return DayPhase.Day;
        }

        if (time >= TimeSpan.FromHours(17.5) && time < TimeSpan.FromHours(21.5))
        {
            return DayPhase.Evening;
        }

        return DayPhase.Night;
    }

    private enum DayPhase
    {
        Day,
        Evening,
        Night
    }
}
