using System.Text.Json;
using LightPilot.Core;

namespace LightPilot.Infrastructure;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonSettingsStore()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LightPilot", "settings.json"))
    {
    }

    public JsonSettingsStore(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public async ValueTask<UserSettings> LoadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return UserSettings.Default;
            }

            try
            {
                await using var stream = File.OpenRead(_settingsPath);
                var settings = await JsonSerializer.DeserializeAsync<UserSettings>(stream, Options, cancellationToken).ConfigureAwait(false);
                return Normalize(settings);
            }
            catch (JsonException)
            {
                QuarantineCorruptFile();
                return UserSettings.Default;
            }
            catch (NotSupportedException)
            {
                QuarantineCorruptFile();
                return UserSettings.Default;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask SaveAsync(UserSettings settings, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = $"{_settingsPath}.tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, settings, Options, cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(_settingsPath))
            {
                File.Replace(tempPath, _settingsPath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, _settingsPath);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private UserSettings Normalize(UserSettings? settings)
    {
        if (settings is null)
        {
            return UserSettings.Default;
        }

        var minimum = Math.Clamp(settings.MinimumBrightnessPercent, 15, 100);
        var maximum = Math.Clamp(settings.MaximumBrightnessPercent, minimum, 100);
        return settings with
        {
            ComfortIntensity = Math.Clamp(settings.ComfortIntensity, 0, 100),
            MinimumBrightnessPercent = minimum,
            MaximumBrightnessPercent = maximum
        };
    }

    private void QuarantineCorruptFile()
    {
        if (!File.Exists(_settingsPath))
        {
            return;
        }

        var corruptPath = $"{_settingsPath}.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        File.Move(_settingsPath, corruptPath, overwrite: true);
    }
}
