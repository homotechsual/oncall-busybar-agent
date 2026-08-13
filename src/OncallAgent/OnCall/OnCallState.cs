namespace OncallAgent.OnCall;

/// <summary>What the BUSY Bar's timer should currently show, as decided by
/// <see cref="OnCallStateEvaluator"/>.</summary>
public abstract record OnCallState;

/// <summary>Not on a call; the device's timer should be stopped.</summary>
public sealed record NotOnCall : OnCallState;

/// <summary>On a call with a known end time (from a matching calendar event) — the timer should
/// be sized to the remaining time until then.</summary>
public sealed record OnCallUntil(DateTimeOffset EndUtc) : OnCallState;

/// <summary>In a focus session with a known end time (from a matching calendar event) — the timer
/// should be sized to the remaining time until then.</summary>
public sealed record FocusingUntil(DateTimeOffset EndUtc) : OnCallState;

/// <summary>In a focus session with no known end time — the timer should run open ended.</summary>
public sealed record FocusingIndefinite : OnCallState;

/// <summary>On a call with no known end time (an ad hoc call, or the local microphone fallback,
/// neither of which carries a duration) — the timer should run open ended.</summary>
public sealed record OnCallIndefinite : OnCallState;
