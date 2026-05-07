using LightPilot.Core;
using LightPilot.Infrastructure;

namespace LightPilot.Infrastructure.Tests;

public sealed class JsonSettingsStoreTests
{
    [Fact]
    public async Task LoadAsyncReturnsDefaultsWhenSettingsFileIsMissing()
    {
        using var temp = new TempDirectory();
        var store = new JsonSettingsStore(Path.Combine(temp.Path, "settings.json"));

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.True(settings.AutoEnabled);
        Assert.False(settings.EnableContentBrightnessAnalysis);
        Assert.Equal(UserSettings.Default.MinimumBrightnessPercent, settings.MinimumBrightnessPercent);
        Assert.Equal(UserSettings.Default.MaximumBrightnessPercent, settings.MaximumBrightnessPercent);
    }

    [Fact]
    public async Task SaveAsyncRoundTripsSettings()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var store = new JsonSettingsStore(path);
        var expected = UserSettings.Default with
        {
            ComfortIntensity = 42,
            EnableDdcCi = false,
            EnableContentBrightnessAnalysis = true,
            MinimumBrightnessPercent = 30
        };

        await store.SaveAsync(expected, CancellationToken.None);
        var actual = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(42, actual.ComfortIntensity);
        Assert.False(actual.EnableDdcCi);
        Assert.True(actual.EnableContentBrightnessAnalysis);
        Assert.Equal(30, actual.MinimumBrightnessPercent);
    }

    [Fact]
    public async Task CorruptFileIsQuarantinedAndDefaultsReturned()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        await File.WriteAllTextAsync(path, "{not-json");
        var store = new JsonSettingsStore(path);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.True(settings.AutoEnabled);
        Assert.False(File.Exists(path));
        Assert.Single(Directory.EnumerateFiles(temp.Path, "settings.json.corrupt-*"));
    }
}

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "light-pilot-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
