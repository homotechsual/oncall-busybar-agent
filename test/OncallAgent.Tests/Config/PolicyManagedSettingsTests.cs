using OncallAgent.Config;
using Xunit;

namespace OncallAgent.Tests.Config;

public class PolicyManagedSettingsTests
{
    [Fact]
    public void From_MarksNothingManaged_WhenThereIsNoPolicy()
    {
        var managed = PolicyManagedSettings.From([]);

        Assert.False(managed.Any);
    }

    [Fact]
    public void From_MarksOnlyTheSettingsPolicySets_IgnoringCase()
    {
        var managed = PolicyManagedSettings.From(
        [
            new PolicyRegistryValue("busybar", "address", "10.0.5.99"),
            new PolicyRegistryValue("Graph", "PollIntervalSeconds", 60)
        ]);

        Assert.True(managed.Address);
        Assert.False(managed.HttpAccessPassword);
        Assert.False(managed.MicEnabled);
        Assert.True(managed.Any);
    }

    [Fact]
    public void From_MarksThePasswordAndMicrophoneManaged()
    {
        var managed = PolicyManagedSettings.From(
        [
            new PolicyRegistryValue("BusyBar", "HttpAccessPassword", "pw"),
            new PolicyRegistryValue("Mic", "Enabled", "false")
        ]);

        Assert.False(managed.Address);
        Assert.True(managed.HttpAccessPassword);
        Assert.True(managed.MicEnabled);
    }
}
