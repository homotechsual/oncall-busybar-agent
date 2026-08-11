using OncallAgent.Config;
using Xunit;

namespace OncallAgent.Tests.Config;

public class UserConfigBootstrapperTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"oncall-agent-tests-{Guid.NewGuid()}");

    [Fact]
    public void EnsureUserConfigExists_CopiesBundledFile_WhenUserConfigMissing()
    {
        Directory.CreateDirectory(_tempDir);
        var bundledPath = Path.Combine(_tempDir, "bundled-appsettings.json");
        var userPath = Path.Combine(_tempDir, "userdir", "appsettings.json");
        File.WriteAllText(bundledPath, "{\"Graph\":{}}");

        UserConfigBootstrapper.EnsureUserConfigExists(bundledPath, userPath);

        Assert.True(File.Exists(userPath));
        Assert.Equal("{\"Graph\":{}}", File.ReadAllText(userPath));
    }

    [Fact]
    public void EnsureUserConfigExists_DoesNotOverwrite_WhenUserConfigAlreadyExists()
    {
        Directory.CreateDirectory(_tempDir);
        var bundledPath = Path.Combine(_tempDir, "bundled-appsettings.json");
        var userPath = Path.Combine(_tempDir, "userdir", "appsettings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(userPath)!);
        File.WriteAllText(bundledPath, "{\"Graph\":{}}");
        File.WriteAllText(userPath, "{\"BusyBar\":{\"Address\":\"192.168.1.1\"}}");

        UserConfigBootstrapper.EnsureUserConfigExists(bundledPath, userPath);

        Assert.Equal("{\"BusyBar\":{\"Address\":\"192.168.1.1\"}}", File.ReadAllText(userPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
