namespace BusyBarOncallAgent.Mic;

public sealed class MicrophoneActivityDetector
{
    private readonly IMicrophoneRegistryReader _reader;

    public MicrophoneActivityDetector(IMicrophoneRegistryReader reader)
    {
        _reader = reader;
    }

    public bool IsActive() => IsActive(_reader.ReadAppUsages());

    /// <summary>Pure decision logic, separated from the registry read so it is unit testable
    /// without touching the real registry. An app is actively using the microphone when its
    /// last-used-stop time is absent (never stopped) or earlier than its last-used-start time (a
    /// new session started since the last recorded stop). Internal (not private) so tests can
    /// drive it directly, matching PollingBackgroundService's own pattern in
    /// psatool-busybar-agent.</summary>
    internal static bool IsActive(IReadOnlyList<MicrophoneAppUsage> usages) =>
        usages.Any(u => u.LastUsedStartUtc is not null &&
            (u.LastUsedStopUtc is null || u.LastUsedStopUtc < u.LastUsedStartUtc));
}
