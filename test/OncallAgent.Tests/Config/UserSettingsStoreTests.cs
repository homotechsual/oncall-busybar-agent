using System.Text.Json;
using System.Text.Json.Nodes;
using OncallAgent.Config;
using Xunit;

namespace OncallAgent.Tests.Config;

public class UserSettingsStoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"oncall-agent-tests-{Guid.NewGuid()}");

    private string UserPath => Path.Combine(_tempDir, "appsettings.json");

    private JsonObject ReadBack() => JsonNode.Parse(File.ReadAllText(UserPath))!.AsObject();

    [Fact]
    public void Save_KeepsKeysTheWindowDoesNotEdit()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(UserPath, """
            {
              // Comments are allowed in the settings file.
              "Graph": { "ClientId": "my-client", "PollIntervalSeconds": 60 },
              "Mic": { "Enabled": true, "PollIntervalSeconds": 5 },
              "BusyBar": { "Address": "10.0.4.20", "HttpAccessPassword": "" },
            }
            """);

        UserSettingsStore.Save(UserPath, new UserSettings("192.168.1.50", null, false));

        var root = ReadBack();
        Assert.Equal("my-client", (string?)root["Graph"]!["ClientId"]);
        Assert.Equal(60, (int?)root["Graph"]!["PollIntervalSeconds"]);
        Assert.Equal(5, (int?)root["Mic"]!["PollIntervalSeconds"]);
        Assert.False((bool?)root["Mic"]!["Enabled"]);
        Assert.Equal("192.168.1.50", (string?)root["BusyBar"]!["Address"]);
    }

    [Fact]
    public void Save_CreatesTheFile_WhenItDoesNotExist()
    {
        UserSettingsStore.Save(UserPath, new UserSettings("10.0.4.20", null, true));

        var root = ReadBack();
        Assert.Equal("10.0.4.20", (string?)root["BusyBar"]!["Address"]);
        Assert.True((bool?)root["Mic"]!["Enabled"]);
    }

    [Fact]
    public void Save_LeavesNullSettingsUntouched()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(UserPath, """{ "BusyBar": { "Address": "bar.local", "HttpAccessPassword": "plain" }, "Mic": { "Enabled": false } }""");

        UserSettingsStore.Save(UserPath, new UserSettings(null, null, null));

        var root = ReadBack();
        Assert.Equal("bar.local", (string?)root["BusyBar"]!["Address"]);
        Assert.Equal("plain", (string?)root["BusyBar"]!["HttpAccessPassword"]);
        Assert.False((bool?)root["Mic"]!["Enabled"]);
    }

    [Fact]
    public void Save_StoresThePasswordEncrypted_AndClearsAnyPlainOne()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(UserPath, """{ "BusyBar": { "HttpAccessPassword": "old-plain" } }""");

        UserSettingsStore.Save(UserPath, new UserSettings(null, "s3cret", null));

        var text = File.ReadAllText(UserPath);
        Assert.DoesNotContain("s3cret", text);
        Assert.DoesNotContain("old-plain", text);
        var busyBar = ReadBack()["BusyBar"]!;
        Assert.Equal("", (string?)busyBar["HttpAccessPassword"]);
        Assert.Equal("s3cret", HttpAccessPasswordProtector.TryUnprotect((string)busyBar["HttpAccessPasswordProtected"]!));
    }

    [Fact]
    public void Save_RemovesTheEncryptedPassword_WhenThePasswordIsCleared()
    {
        UserSettingsStore.Save(UserPath, new UserSettings(null, "s3cret", null));

        UserSettingsStore.Save(UserPath, new UserSettings(null, "", null));

        var busyBar = ReadBack()["BusyBar"]!.AsObject();
        Assert.False(busyBar.ContainsKey("HttpAccessPasswordProtected"));
        Assert.Equal("", (string?)busyBar["HttpAccessPassword"]);
    }

    [Fact]
    public void Save_UpdatesExistingKeys_WhateverTheirCase()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(UserPath, """{ "busybar": { "address": "old" } }""");

        UserSettingsStore.Save(UserPath, new UserSettings("new", null, null));

        var root = ReadBack();
        Assert.Single(root);
        var busyBar = root["busybar"]!.AsObject();
        Assert.Single(busyBar);
        Assert.Equal("new", (string?)busyBar["Address"]);
    }

    [Fact]
    public void Save_Throws_AndLeavesTheFileAlone_WhenItIsNotValidJson()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(UserPath, "{ not json");

        Assert.ThrowsAny<JsonException>(() => UserSettingsStore.Save(UserPath, new UserSettings("x", null, null)));
        Assert.Equal("{ not json", File.ReadAllText(UserPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
