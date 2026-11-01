using OncallAgent.Graph;

namespace OncallAgent.OnCall;

/// <summary>Pure function turning the raw signals into an <see cref="OnCallState"/>. No I/O.
/// Microsoft Graph presence is the primary signal; the microphone is consulted only when
/// presence itself does not already indicate a call. It's a fallback for what presence misses, and on
/// its own it never carries a known end time.
///
/// InAMeeting is the exception: Teams derives it from the calendar for the whole scheduled slot,
/// joined or not, so it only counts as a call while the microphone is in use. Otherwise leaving a
/// call early would keep the timer running until the meeting's scheduled end.</summary>
public static class OnCallStateEvaluator
{
    private static readonly HashSet<string> OnCallActivities = new(StringComparer.OrdinalIgnoreCase)
    {
        "InACall",
        "InAConferenceCall",
        "Presenting"
    };
    private const string InAMeetingActivity = "InAMeeting";
    private const string FocusingAvailability = "Focusing";

    public static OnCallState Evaluate(GraphPresenceSnapshot? presence, DateTimeOffset? currentMeetingEndUtc, bool microphoneActive)
    {
        if (presence is not null && OnCallActivities.Contains(presence.Activity))
        {
            return currentMeetingEndUtc is { } end ? new OnCallUntil(end) : new OnCallIndefinite();
        }

        if (microphoneActive)
        {
            var inScheduledMeeting = presence is not null
                && string.Equals(presence.Activity, InAMeetingActivity, StringComparison.OrdinalIgnoreCase);
            return inScheduledMeeting && currentMeetingEndUtc is { } end ? new OnCallUntil(end) : new OnCallIndefinite();
        }

        if (presence is not null && string.Equals(presence.Availability, FocusingAvailability, StringComparison.OrdinalIgnoreCase))
        {
            return currentMeetingEndUtc is { } end ? new FocusingUntil(end) : new FocusingIndefinite();
        }

        return new NotOnCall();
    }
}
