using OncallAgent.Graph;

namespace OncallAgent.OnCall;

/// <summary>Pure function turning the raw signals into an <see cref="OnCallState"/>. No I/O.
/// Microsoft Graph presence is the primary signal; the microphone is consulted only when
/// presence itself does not already indicate a call — a fallback for what presence misses, never
/// a second independent trigger, and it never carries a known end time.</summary>
public static class OnCallStateEvaluator
{
    private static readonly string[] OnCallActivities = ["InACall", "InAMeeting", "Presenting"];
    private const string FocusingAvailability = "Focusing";

    public static OnCallState Evaluate(GraphPresenceSnapshot? presence, DateTimeOffset? currentMeetingEndUtc, bool microphoneActive)
    {
        if (presence is not null && OnCallActivities.Contains(presence.Activity))
        {
            return currentMeetingEndUtc is { } end ? new OnCallUntil(end) : new OnCallIndefinite();
        }

        if (microphoneActive)
        {
            return new OnCallIndefinite();
        }

        if (presence is not null && string.Equals(presence.Availability, FocusingAvailability, StringComparison.OrdinalIgnoreCase))
        {
            return currentMeetingEndUtc is { } end ? new FocusingUntil(end) : new FocusingIndefinite();
        }

        return new NotOnCall();
    }
}
