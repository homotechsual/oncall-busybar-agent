namespace OncallAgent.Mic;

public sealed class MicrophoneActivityDetector
{
    // A "never stopped" entry does not reliably mean an app is still recording right now: Windows
    // leaves this permanently unset for any executable that never runs again. For example, an
    // auto-updater (Discord, etc.) that moves to a new versioned exe path on every update abandons
    // the old path's registry entry mid-session, sometimes for months. Confirmed live: a stale
    // entry like that, months old, kept this permanently "active" long after any real call ended.
    // Bounding to a generous recency window excludes orphaned entries like that while still
    // covering any realistically long real call.
    private static readonly TimeSpan StalenessThreshold = TimeSpan.FromHours(24);

    private readonly IMicrophoneRegistryReader _reader;

    public MicrophoneActivityDetector(IMicrophoneRegistryReader reader)
    {
        _reader = reader;
    }

    public bool IsActive() => IsActive(_reader.ReadAppUsages(), DateTimeOffset.UtcNow);

    /// <summary>Pure decision logic, separated from the registry read so it is unit testable
    /// without touching the real registry. An app is actively using the microphone when it
    /// started within <see cref="StalenessThreshold"/> of now, and its last-used-stop time is
    /// either absent (never stopped) or earlier than its last-used-start time (a new session
    /// started since the last recorded stop). Internal (not private) so tests can drive it
    /// directly, matching PollingBackgroundService's own pattern in psatool-busybar-agent.</summary>
    internal static bool IsActive(IReadOnlyList<MicrophoneAppUsage> usages, DateTimeOffset now) =>
        usages.Any(u => u.LastUsedStartUtc is { } start &&
            now - start < StalenessThreshold &&
            (u.LastUsedStopUtc is null || u.LastUsedStopUtc < start));
}
