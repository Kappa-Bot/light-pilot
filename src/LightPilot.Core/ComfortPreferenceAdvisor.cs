namespace LightPilot.Core;

public enum ComfortFeedback
{
    TooBright,
    TooDim
}

public static class ComfortPreferenceAdvisor
{
    private const int FeedbackStep = 5;

    public static UserSettings Apply(UserSettings settings, ComfortFeedback feedback)
    {
        var delta = feedback == ComfortFeedback.TooBright ? FeedbackStep : -FeedbackStep;
        return settings with
        {
            AutoEnabled = true,
            ComfortIntensity = Math.Clamp(settings.ComfortIntensity + delta, 0, 100)
        };
    }
}
