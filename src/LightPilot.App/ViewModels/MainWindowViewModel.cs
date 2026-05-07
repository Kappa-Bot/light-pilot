using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using LightPilot.Core;
using LightPilot.Infrastructure;

namespace LightPilot.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly AdaptiveEngine _engine = new();
    private readonly ISettingsStore _settingsStore;
    private readonly IMonitorEnumerator _monitorEnumerator;
    private readonly IForegroundWindowDetector _foregroundWindowDetector;
    private readonly IContentLuminanceSampler _contentLuminanceSampler;
    private readonly IBrightnessController _brightnessController;
    private readonly DispatcherTimer _timer;
    private readonly List<MonitorModel> _monitorModels = [];
    private readonly Dictionary<string, AdaptiveEngineState> _engineStates = new(StringComparer.OrdinalIgnoreCase);

    private UserSettings _settings = UserSettings.Default;
    private DateTimeOffset? _pauseUntil;
    private ComfortProfileId _currentMode = ComfortProfileId.Auto;
    private int _brightnessPercent;
    private int _colorTemperatureKelvin;
    private string _reason = "Starting Light Pilot";
    private string _autoStatus = "Auto on";
    private string _transitionText = "";
    private bool _startWithWindows;
    private bool _refreshInProgress;
    private CancellationTokenSource? _settingsSaveCts;

    public MainWindowViewModel(
        ISettingsStore settingsStore,
        IMonitorEnumerator monitorEnumerator,
        IForegroundWindowDetector foregroundWindowDetector,
        IContentLuminanceSampler contentLuminanceSampler,
        IBrightnessController brightnessController)
    {
        _settingsStore = settingsStore;
        _monitorEnumerator = monitorEnumerator;
        _foregroundWindowDetector = foregroundWindowDetector;
        _contentLuminanceSampler = contentLuminanceSampler;
        _brightnessController = brightnessController;

        Monitors = [];

        ToggleAutoCommand = new RelayCommand(ToggleAuto);
        PauseCommand = new RelayCommand(PauseThirtyMinutes);
        PauseThirtyMinutesCommand = new RelayCommand(PauseThirtyMinutes);
        PauseOneHourCommand = new RelayCommand(PauseOneHour);
        PauseUntilTomorrowCommand = new RelayCommand(PauseUntilTomorrow);
        ResumeCommand = new RelayCommand(ResumeAuto);
        ResetCommand = new RelayCommand(ResetDefaults);
        SetCalmCommand = new RelayCommand(() => SetIntensityPreset(35));
        SetBalancedCommand = new RelayCommand(() => SetIntensityPreset(50));
        SetDeepComfortCommand = new RelayCommand(() => SetIntensityPreset(70));
        OpenSettingsCommand = new RelayCommand(() => RequestSettings?.Invoke(this, EventArgs.Empty));
        ExitCommand = new RelayCommand(() => RequestExit?.Invoke(this, EventArgs.Empty));

        _timer = new DispatcherTimer { Interval = RefreshCadencePolicy.GetInterval(_settings, isPaused: false, isContentAnalysisEnabled: false) };
        _timer.Tick += async (_, _) => await RefreshDecisionAsync().ConfigureAwait(true);

        _ = InitializeAsync();
    }

    public event EventHandler? RequestSettings;
    public event EventHandler? RequestExit;

    public ObservableCollection<MonitorStatusViewModel> Monitors { get; }

    public RelayCommand ToggleAutoCommand { get; }
    public RelayCommand PauseCommand { get; }
    public RelayCommand PauseThirtyMinutesCommand { get; }
    public RelayCommand PauseOneHourCommand { get; }
    public RelayCommand PauseUntilTomorrowCommand { get; }
    public RelayCommand ResumeCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand SetCalmCommand { get; }
    public RelayCommand SetBalancedCommand { get; }
    public RelayCommand SetDeepComfortCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand ExitCommand { get; }

    public UserSettings Settings => _settings;

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetProperty(ref _startWithWindows, value);
    }

    public bool AutoEnabled
    {
        get => _settings.AutoEnabled;
        set
        {
            if (_settings.AutoEnabled == value)
            {
                return;
            }

            if (value)
            {
                _pauseUntil = null;
            }

            UpdateSettings(_settings with { AutoEnabled = value }, persist: true);
        }
    }

    public int ComfortIntensity
    {
        get => _settings.ComfortIntensity;
        set
        {
            var clamped = Math.Clamp(value, 0, 100);
            if (_settings.ComfortIntensity != clamped)
            {
                UpdateSettings(_settings with { ComfortIntensity = clamped }, persist: true);
            }
        }
    }

    public ComfortProfileId CurrentMode
    {
        get => _currentMode;
        private set => SetProperty(ref _currentMode, value);
    }

    public int BrightnessPercent
    {
        get => _brightnessPercent;
        private set => SetProperty(ref _brightnessPercent, value);
    }

    public int ColorTemperatureKelvin
    {
        get => _colorTemperatureKelvin;
        private set => SetProperty(ref _colorTemperatureKelvin, value);
    }

    public string Reason
    {
        get => _reason;
        private set => SetProperty(ref _reason, value);
    }

    public string AutoStatus
    {
        get => _autoStatus;
        private set => SetProperty(ref _autoStatus, value);
    }

    public string TransitionText
    {
        get => _transitionText;
        private set => SetProperty(ref _transitionText, value);
    }

    public string BrightnessText => $"{BrightnessPercent}%";

    public string WarmthText => ColorTemperatureKelvin >= 5000 ? "Neutral" : ColorTemperatureKelvin >= 4000 ? "Soft" : "Warm";

    public string CurrentModeText => CurrentMode switch
    {
        ComfortProfileId.Reading when GetDayPart() == "Evening" => "Evening Reading",
        ComfortProfileId.Reading => "Reading",
        ComfortProfileId.Development when GetDayPart() == "Night" => "Late Development",
        ComfortProfileId.Evening => "Evening",
        ComfortProfileId.Night => "Night",
        ComfortProfileId.Day => "Day",
        _ => CurrentMode.ToString()
    };

    public void ApplySettings(UserSettings settings, bool startWithWindows)
    {
        StartWithWindows = startWithWindows;
        UpdateSettings(settings, persist: false);
        _ = PersistSettingsAsync(settings, immediate: true);
    }

    private async Task InitializeAsync()
    {
        try
        {
            _settings = await _settingsStore.LoadAsync(CancellationToken.None).ConfigureAwait(true);
            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(AutoEnabled));
            OnPropertyChanged(nameof(ComfortIntensity));
            UpdateTimerInterval();
            await ReloadMonitorsAsync().ConfigureAwait(true);
            await RefreshDecisionAsync().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Reason = "Settings unavailable; using safe defaults";
        }

        _timer.Start();
    }

    private async Task ReloadMonitorsAsync()
    {
        IReadOnlyList<MonitorModel> monitors;
        try
        {
            monitors = await _monitorEnumerator.EnumerateAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            monitors = [new MonitorModel("primary", "Primary display", false, true, 15, 100, 0)];
        }

        _monitorModels.Clear();
        _monitorModels.AddRange(monitors);
        Monitors.Clear();
        foreach (var monitor in _monitorModels)
        {
            Monitors.Add(new MonitorStatusViewModel(monitor.Name));
            _engineStates.TryAdd(monitor.Id, AdaptiveEngineState.Empty);
        }
    }

    public async Task RefreshDecisionAsync()
    {
        if (_refreshInProgress)
        {
            return;
        }

        _refreshInProgress = true;
        try
        {
            if (_pauseUntil <= DateTimeOffset.Now)
            {
                _pauseUntil = null;
            }

            if (_monitorModels.Count == 0)
            {
                await ReloadMonitorsAsync().ConfigureAwait(true);
            }

            var isPaused = _pauseUntil is not null;
            var effectiveSettings = isPaused ? _settings with { AutoEnabled = false } : _settings;
            var appContext = effectiveSettings.AutoEnabled
                ? await Task.Run(() => SafeDetectContext()).ConfigureAwait(true)
                : new AppContextModel("LightPilot.App", AppCategory.System, false);
            var content = effectiveSettings.AutoEnabled
                ? await SampleContentAsync(effectiveSettings.EnableContentBrightnessAnalysis).ConfigureAwait(true)
                : ContentLuminanceSample.Unknown;

            ComfortDecision? primaryDecision = null;
            for (var index = 0; index < _monitorModels.Count; index++)
            {
                var monitor = _monitorModels[index];
                var currentBrightness = BrightnessPercent == 0 ? 62 : BrightnessPercent;
                var currentKelvin = ColorTemperatureKelvin == 0 ? 5200 : ColorTemperatureKelvin;
                var snapshot = CreateSnapshot(monitor, appContext, content, currentBrightness, currentKelvin);
                var state = _engineStates.GetValueOrDefault(monitor.Id, AdaptiveEngineState.Empty);
                var decision = _engine.Evaluate(snapshot, state, effectiveSettings);

                await _brightnessController.ApplyAsync(monitor, decision, effectiveSettings, CancellationToken.None).ConfigureAwait(true);

                if (decision.ShouldApply)
                {
                    _engineStates[monitor.Id] = state with
                    {
                        LastAppliedAt = snapshot.Now,
                        LastDecision = decision.Target
                    };
                }

                UpdateMonitorStatus(index, monitor, decision, effectiveSettings);
                primaryDecision ??= decision;
            }

            if (primaryDecision is not null)
            {
                CurrentMode = primaryDecision.Profile;
                BrightnessPercent = primaryDecision.TargetBrightnessPercent;
                ColorTemperatureKelvin = primaryDecision.TargetColorTemperatureKelvin;
                Reason = primaryDecision.Reason;
                TransitionText = $"{(int)primaryDecision.TransitionDuration.TotalSeconds}s transition";
            }

            AutoStatus = BuildAutoStatus();
            UpdateTimerInterval();
            OnPropertyChanged(nameof(BrightnessText));
            OnPropertyChanged(nameof(WarmthText));
            OnPropertyChanged(nameof(CurrentModeText));
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    private AppContextModel SafeDetectContext()
    {
        try
        {
            return _foregroundWindowDetector.Detect();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new AppContextModel("unknown.exe", AppCategory.Unknown, false);
        }
    }

    private async Task<ContentLuminanceSample> SampleContentAsync(bool enabled)
    {
        try
        {
            return await Task.Run(async () => await _contentLuminanceSampler.SampleAsync(enabled, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ExternalException)
        {
            return ContentLuminanceSample.Unknown;
        }
    }

    private AdaptiveSnapshot CreateSnapshot(MonitorModel monitor, AppContextModel appContext, ContentLuminanceSample content, int currentBrightness, int currentKelvin)
    {
        return new AdaptiveSnapshot(
            DateTimeOffset.Now,
            monitor,
            appContext,
            content,
            TimeSpan.FromMinutes(42),
            currentBrightness,
            currentKelvin,
            _pauseUntil);
    }

    private void UpdateMonitorStatus(int index, MonitorModel monitor, ComfortDecision decision, UserSettings effectiveSettings)
    {
        var status = Monitors[index];
        status.BrightnessPercent = decision.TargetBrightnessPercent;
        status.ColorTemperatureKelvin = monitor.SupportsColorTemperature ? decision.TargetColorTemperatureKelvin : 6500;
        status.ControlLayer = GetControlLayer(monitor, effectiveSettings);
        status.Status = effectiveSettings.AutoEnabled && _pauseUntil is null ? "Auto ready" : "Held";
    }

    private string BuildAutoStatus()
    {
        if (!_settings.AutoEnabled)
        {
            return "Auto off";
        }

        if (_pauseUntil is { } until)
        {
            return $"Paused until {until.LocalDateTime:t}";
        }

        return "Auto is active";
    }

    private static string GetControlLayer(MonitorModel monitor, UserSettings settings)
    {
        if (settings.EnableDdcCi && monitor.SupportsBrightnessControl)
        {
            return "DDC/CI";
        }

        return monitor.SupportsBrightnessControl ? "Windows brightness" : "Overlay fallback";
    }

    private void ToggleAuto()
    {
        AutoEnabled = !AutoEnabled;
    }

    private void PauseThirtyMinutes()
    {
        if (!_settings.AutoEnabled)
        {
            AutoEnabled = true;
        }

        _pauseUntil = DateTimeOffset.Now.AddMinutes(30);
        UpdateTimerInterval();
        _ = RefreshDecisionAsync();
    }

    private void PauseOneHour()
    {
        if (!_settings.AutoEnabled)
        {
            AutoEnabled = true;
        }

        _pauseUntil = DateTimeOffset.Now.AddHours(1);
        UpdateTimerInterval();
        _ = RefreshDecisionAsync();
    }

    private void PauseUntilTomorrow()
    {
        if (!_settings.AutoEnabled)
        {
            AutoEnabled = true;
        }

        var tomorrowWake = DateTime.Today.AddDays(1).Add(_settings.WakeTime.ToTimeSpan());
        _pauseUntil = new DateTimeOffset(tomorrowWake);
        UpdateTimerInterval();
        _ = RefreshDecisionAsync();
    }

    private void ResumeAuto()
    {
        _pauseUntil = null;
        if (!_settings.AutoEnabled)
        {
            _settings = _settings with { AutoEnabled = true };
            _ = PersistSettingsAsync(_settings, immediate: false);
        }

        OnPropertyChanged(nameof(AutoEnabled));
        UpdateTimerInterval();
        _ = RefreshDecisionAsync();
    }

    private void ResetDefaults()
    {
        _pauseUntil = null;
        UpdateSettings(UserSettings.Default, persist: true);
    }

    private void SetIntensityPreset(int intensity)
    {
        ComfortIntensity = intensity;
    }

    private void UpdateSettings(UserSettings settings, bool persist)
    {
        _settings = settings;
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(AutoEnabled));
        OnPropertyChanged(nameof(ComfortIntensity));

        if (persist)
        {
            _ = PersistSettingsAsync(settings, immediate: false);
        }

        UpdateTimerInterval();
        _ = RefreshDecisionAsync();
    }

    private async Task PersistSettingsAsync(UserSettings settings, bool immediate)
    {
        _settingsSaveCts?.Cancel();
        _settingsSaveCts?.Dispose();
        var cts = new CancellationTokenSource();
        _settingsSaveCts = cts;

        try
        {
            if (!immediate)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(750), cts.Token).ConfigureAwait(false);
            }

            await _settingsStore.SaveAsync(settings, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
            Reason = "Settings could not be saved";
        }
        catch (UnauthorizedAccessException)
        {
            Reason = "Settings could not be saved";
        }
    }

    private void UpdateTimerInterval()
    {
        var interval = RefreshCadencePolicy.GetInterval(_settings, _pauseUntil is not null, _settings.EnableContentBrightnessAnalysis);
        if (_timer.Interval != interval)
        {
            _timer.Interval = interval;
        }
    }

    private static string GetDayPart()
    {
        var now = DateTimeOffset.Now.TimeOfDay;
        if (now >= TimeSpan.FromHours(17.5) && now < TimeSpan.FromHours(21.5))
        {
            return "Evening";
        }

        return now < TimeSpan.FromHours(8) || now >= TimeSpan.FromHours(21.5) ? "Night" : "Day";
    }
}
