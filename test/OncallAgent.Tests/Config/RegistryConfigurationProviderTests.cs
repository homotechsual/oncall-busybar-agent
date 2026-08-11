using OncallAgent.Config;
using Xunit;

namespace OncallAgent.Tests.Config;

public class RegistryConfigurationProviderTests
{
    [Fact]
    public void Load_PopulatesDataWithSectionColonKeyFormat()
    {
        var reader = new FakePolicyRegistryReader();
        reader.Values.Add(new PolicyRegistryValue("BusyBar", "Address", "10.0.5.99"));
        var provider = new RegistryConfigurationProvider(reader);

        provider.Load();

        Assert.True(provider.TryGet("BusyBar:Address", out var value));
        Assert.Equal("10.0.5.99", value);
    }

    [Fact]
    public void Load_ConvertsNonStringValuesToString()
    {
        var reader = new FakePolicyRegistryReader();
        reader.Values.Add(new PolicyRegistryValue("Graph", "PollIntervalSeconds", 45));
        var provider = new RegistryConfigurationProvider(reader);

        provider.Load();

        Assert.True(provider.TryGet("Graph:PollIntervalSeconds", out var value));
        Assert.Equal("45", value);
    }

    [Fact]
    public void Load_ProducesNoEntries_WhenReaderReturnsNoValues()
    {
        var provider = new RegistryConfigurationProvider(new FakePolicyRegistryReader());

        provider.Load();

        Assert.False(provider.TryGet("Graph:ClientId", out _));
    }

    [Fact]
    public void Load_KeysAreCaseInsensitive()
    {
        var reader = new FakePolicyRegistryReader();
        reader.Values.Add(new PolicyRegistryValue("Mic", "Enabled", "false"));
        var provider = new RegistryConfigurationProvider(reader);

        provider.Load();

        Assert.True(provider.TryGet("mic:enabled", out var value));
        Assert.Equal("false", value);
    }
}
