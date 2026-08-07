namespace OncallAgent.Mic;

/// <summary>One application's recorded microphone access, as read from the registry. Null start
/// or stop times mean that value was never recorded (the app has never used the microphone, or
/// Windows has not written a stop time yet).</summary>
public sealed record MicrophoneAppUsage(string AppId, DateTimeOffset? LastUsedStartUtc, DateTimeOffset? LastUsedStopUtc);
