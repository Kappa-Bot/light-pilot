using LightPilot.Infrastructure;

namespace LightPilot.Infrastructure.Tests;

public sealed class StartupRegistrationServiceTests
{
    [Fact]
    public void BuildStartupCommandQuotesExecutableAndAddsBackgroundFlag()
    {
        var command = StartupRegistrationService.BuildStartupCommand(@"C:\Users\edfpo\App Data\LightPilot\LightPilot.App.exe");

        Assert.Equal("\"C:\\Users\\edfpo\\App Data\\LightPilot\\LightPilot.App.exe\" --background", command);
    }

    [Fact]
    public void BuildStartupCommandRejectsEmptyPath()
    {
        Assert.Throws<ArgumentException>(() => StartupRegistrationService.BuildStartupCommand(""));
    }

    [Fact]
    public void BuildStartupCommandRejectsEmbeddedQuotes()
    {
        Assert.Throws<ArgumentException>(() => StartupRegistrationService.BuildStartupCommand("C:\\bad\"path\\app.exe"));
    }
}
