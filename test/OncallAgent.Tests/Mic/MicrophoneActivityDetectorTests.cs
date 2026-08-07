using OncallAgent.Mic;
using Xunit;

namespace OncallAgent.Tests.Mic;

public class MicrophoneActivityDetectorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void IsActive_ReturnsFalse_WhenNoUsagesRecorded()
    {
        var result = MicrophoneActivityDetector.IsActive([], Now);

        Assert.False(result);
    }

    [Fact]
    public void IsActive_ReturnsTrue_WhenAnAppHasNeverStopped()
    {
        var usages = new[]
        {
            new MicrophoneAppUsage("Teams", Now.AddMinutes(-5), LastUsedStopUtc: null)
        };

        var result = MicrophoneActivityDetector.IsActive(usages, Now);

        Assert.True(result);
    }

    [Fact]
    public void IsActive_ReturnsTrue_WhenTheMostRecentStartIsAfterTheMostRecentStop()
    {
        var usages = new[]
        {
            new MicrophoneAppUsage("Teams", Now.AddMinutes(-1), Now.AddMinutes(-10))
        };

        var result = MicrophoneActivityDetector.IsActive(usages, Now);

        Assert.True(result);
    }

    [Fact]
    public void IsActive_ReturnsFalse_WhenTheAppHasStoppedSinceItLastStarted()
    {
        var usages = new[]
        {
            new MicrophoneAppUsage("Teams", Now.AddMinutes(-10), Now.AddMinutes(-1))
        };

        var result = MicrophoneActivityDetector.IsActive(usages, Now);

        Assert.False(result);
    }

    [Fact]
    public void IsActive_ReturnsTrue_WhenAnyOneOfSeveralAppsIsActive()
    {
        var usages = new[]
        {
            new MicrophoneAppUsage("Discord", Now.AddMinutes(-10), Now.AddMinutes(-1)),
            new MicrophoneAppUsage("Teams", Now.AddMinutes(-2), LastUsedStopUtc: null)
        };

        var result = MicrophoneActivityDetector.IsActive(usages, Now);

        Assert.True(result);
    }

    [Fact]
    public void IsActive_ReturnsFalse_WhenAnAppHasNeverStarted()
    {
        var usages = new[]
        {
            new MicrophoneAppUsage("Teams", LastUsedStartUtc: null, LastUsedStopUtc: null)
        };

        var result = MicrophoneActivityDetector.IsActive(usages, Now);

        Assert.False(result);
    }

    [Fact]
    public void IsActive_ReturnsFalse_WhenAnAppStartedButNeverStoppedMonthsAgo()
    {
        // Reproduces a real bug: an old Discord version's mic-consent registry entry, orphaned by
        // Discord's auto-updater moving to a new versioned exe path, had LastUsedTimeStart from six
        // months ago and no LastUsedTimeStop ever recorded — permanently satisfying the old
        // unbounded "never stopped means active" rule, long after any real call had ended.
        var usages = new[]
        {
            new MicrophoneAppUsage("Discord", Now.AddMonths(-6), LastUsedStopUtc: null)
        };

        var result = MicrophoneActivityDetector.IsActive(usages, Now);

        Assert.False(result);
    }

    [Fact]
    public void IsActive_ReturnsTrue_ForAGenuinelyLongOngoingCall()
    {
        var usages = new[]
        {
            new MicrophoneAppUsage("Teams", Now.AddHours(-4), LastUsedStopUtc: null)
        };

        var result = MicrophoneActivityDetector.IsActive(usages, Now);

        Assert.True(result);
    }
}
