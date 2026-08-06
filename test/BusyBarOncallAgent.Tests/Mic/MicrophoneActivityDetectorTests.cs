using BusyBarOncallAgent.Mic;
using Xunit;

namespace BusyBarOncallAgent.Tests.Mic;

public class MicrophoneActivityDetectorTests
{
    [Fact]
    public void IsActive_ReturnsFalse_WhenNoUsagesRecorded()
    {
        var result = MicrophoneActivityDetector.IsActive([]);

        Assert.False(result);
    }

    [Fact]
    public void IsActive_ReturnsTrue_WhenAnAppHasNeverStopped()
    {
        var usages = new[]
        {
            new MicrophoneAppUsage("Teams", DateTimeOffset.UtcNow.AddMinutes(-5), LastUsedStopUtc: null)
        };

        var result = MicrophoneActivityDetector.IsActive(usages);

        Assert.True(result);
    }

    [Fact]
    public void IsActive_ReturnsTrue_WhenTheMostRecentStartIsAfterTheMostRecentStop()
    {
        var usages = new[]
        {
            new MicrophoneAppUsage("Teams", DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(-10))
        };

        var result = MicrophoneActivityDetector.IsActive(usages);

        Assert.True(result);
    }

    [Fact]
    public void IsActive_ReturnsFalse_WhenTheAppHasStoppedSinceItLastStarted()
    {
        var usages = new[]
        {
            new MicrophoneAppUsage("Teams", DateTimeOffset.UtcNow.AddMinutes(-10), DateTimeOffset.UtcNow.AddMinutes(-1))
        };

        var result = MicrophoneActivityDetector.IsActive(usages);

        Assert.False(result);
    }

    [Fact]
    public void IsActive_ReturnsTrue_WhenAnyOneOfSeveralAppsIsActive()
    {
        var usages = new[]
        {
            new MicrophoneAppUsage("Discord", DateTimeOffset.UtcNow.AddMinutes(-10), DateTimeOffset.UtcNow.AddMinutes(-1)),
            new MicrophoneAppUsage("Teams", DateTimeOffset.UtcNow.AddMinutes(-2), LastUsedStopUtc: null)
        };

        var result = MicrophoneActivityDetector.IsActive(usages);

        Assert.True(result);
    }

    [Fact]
    public void IsActive_ReturnsFalse_WhenAnAppHasNeverStarted()
    {
        var usages = new[]
        {
            new MicrophoneAppUsage("Teams", LastUsedStartUtc: null, LastUsedStopUtc: null)
        };

        var result = MicrophoneActivityDetector.IsActive(usages);

        Assert.False(result);
    }
}
