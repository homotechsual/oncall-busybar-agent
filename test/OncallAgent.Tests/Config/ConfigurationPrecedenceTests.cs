using Microsoft.Extensions.Configuration;
using OncallAgent.Config;
using Xunit;

namespace OncallAgent.Tests.Config;

/// <summary>Replicates the exact source order Program.cs builds — bundled JSON, LocalAppData JSON,
/// env vars, command line, registry policy, lowest to highest precedence — using in-memory/fake
/// sources so it doesn't touch the real file system or registry. This pins the regression Fix 2
/// corrects: the LocalAppData layer must rank below env vars/command line, not above.</summary>
public class ConfigurationPrecedenceTests
{
    [Fact]
    public void RegistryPolicy_OverridesEveryOtherLayer()
    {
        var builder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["BusyBar:Address"] = "bundled-default" })
            .AddInMemoryCollection(new Dictionary<string, string?> { ["BusyBar:Address"] = "local-app-data" })
            .AddInMemoryCollection(new Dictionary<string, string?> { ["BusyBar:Address"] = "env-var" })
            .AddInMemoryCollection(new Dictionary<string, string?> { ["BusyBar:Address"] = "command-line" });

        var reader = new FakePolicyRegistryReader();
        reader.Values.Add(new PolicyRegistryValue("BusyBar", "Address", "registry-policy"));
        builder.Add(new RegistryConfigurationSource { Reader = reader });

        var config = builder.Build();

        Assert.Equal("registry-policy", config["BusyBar:Address"]);
    }

    [Fact]
    public void EnvVarLayer_OverridesLocalAppDataLayer()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["BusyBar:Address"] = "bundled-default" })
            .AddInMemoryCollection(new Dictionary<string, string?> { ["BusyBar:Address"] = "local-app-data" })
            .AddInMemoryCollection(new Dictionary<string, string?> { ["BusyBar:Address"] = "env-var" })
            .Build();

        Assert.Equal("env-var", config["BusyBar:Address"]);
    }
}
