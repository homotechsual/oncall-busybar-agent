using OncallAgent.Graph;
using OncallAgent.OnCall;
using Xunit;

namespace OncallAgent.Tests.OnCall;

public class OnCallStateEvaluatorTests
{
    [Fact]
    public void Evaluate_ReturnsNotOnCall_WhenNoSignalsIndicateACall()
    {
        var presence = new GraphPresenceSnapshot(Availability: "Available", Activity: "Available");

        var result = OnCallStateEvaluator.Evaluate(presence, currentMeetingEndUtc: null, microphoneActive: false);

        Assert.IsType<NotOnCall>(result);
    }

    [Theory]
    [InlineData("InACall")]
    [InlineData("InAMeeting")]
    [InlineData("Presenting")]
    public void Evaluate_ReturnsOnCallIndefinite_WhenPresenceIndicatesACall_WithNoMatchingMeeting(string activity)
    {
        var presence = new GraphPresenceSnapshot(Availability: "Busy", Activity: activity);

        var result = OnCallStateEvaluator.Evaluate(presence, currentMeetingEndUtc: null, microphoneActive: false);

        Assert.IsType<OnCallIndefinite>(result);
    }

    [Fact]
    public void Evaluate_ReturnsOnCallUntilMeetingEnd_WhenPresenceIndicatesACall_WithAMatchingMeeting()
    {
        var presence = new GraphPresenceSnapshot(Availability: "Busy", Activity: "InAMeeting");
        var meetingEnd = DateTimeOffset.UtcNow.AddMinutes(15);

        var result = OnCallStateEvaluator.Evaluate(presence, currentMeetingEndUtc: meetingEnd, microphoneActive: false);

        var until = Assert.IsType<OnCallUntil>(result);
        Assert.Equal(meetingEnd, until.EndUtc);
    }

    [Fact]
    public void Evaluate_ReturnsFocusingIndefinite_WhenPresenceIndicatesFocus_WithNoMatchingCalendarBlock()
    {
        var presence = new GraphPresenceSnapshot(Availability: "Focusing", Activity: "DoNotDisturb");

        var result = OnCallStateEvaluator.Evaluate(presence, currentMeetingEndUtc: null, microphoneActive: false);

        Assert.IsType<FocusingIndefinite>(result);
    }

    [Fact]
    public void Evaluate_ReturnsFocusingUntil_WhenPresenceIndicatesFocus_WithAMatchingCalendarBlock()
    {
        var presence = new GraphPresenceSnapshot(Availability: "Focusing", Activity: "DoNotDisturb");
        var focusEnd = DateTimeOffset.UtcNow.AddMinutes(45);

        var result = OnCallStateEvaluator.Evaluate(presence, currentMeetingEndUtc: focusEnd, microphoneActive: false);

        var until = Assert.IsType<FocusingUntil>(result);
        Assert.Equal(focusEnd, until.EndUtc);
    }

    [Fact]
    public void Evaluate_OnCallPresenceWinsOverFocusingAvailability()
    {
        var presence = new GraphPresenceSnapshot(Availability: "Focusing", Activity: "InACall");
        var meetingEnd = DateTimeOffset.UtcNow.AddMinutes(5);

        var result = OnCallStateEvaluator.Evaluate(presence, currentMeetingEndUtc: meetingEnd, microphoneActive: false);

        var until = Assert.IsType<OnCallUntil>(result);
        Assert.Equal(meetingEnd, until.EndUtc);
    }

    [Fact]
    public void Evaluate_IgnoresMeetingEnd_WhenPresenceDoesNotIndicateACall()
    {
        var presence = new GraphPresenceSnapshot(Availability: "Available", Activity: "Available");
        var meetingEnd = DateTimeOffset.UtcNow.AddMinutes(15);

        var result = OnCallStateEvaluator.Evaluate(presence, currentMeetingEndUtc: meetingEnd, microphoneActive: false);

        Assert.IsType<NotOnCall>(result);
    }

    [Fact]
    public void Evaluate_ReturnsOnCallIndefinite_WhenMicActive_AndPresenceDoesNotIndicateACall()
    {
        var presence = new GraphPresenceSnapshot(Availability: "Available", Activity: "Available");

        var result = OnCallStateEvaluator.Evaluate(presence, currentMeetingEndUtc: null, microphoneActive: true);

        Assert.IsType<OnCallIndefinite>(result);
    }

    [Fact]
    public void Evaluate_ReturnsOnCallIndefinite_WhenMicActive_AndPresenceIsNull()
    {
        var result = OnCallStateEvaluator.Evaluate(presence: null, currentMeetingEndUtc: null, microphoneActive: true);

        Assert.IsType<OnCallIndefinite>(result);
    }

    [Fact]
    public void Evaluate_ReturnsNotOnCall_WhenPresenceIsNull_AndMicInactive()
    {
        var result = OnCallStateEvaluator.Evaluate(presence: null, currentMeetingEndUtc: null, microphoneActive: false);

        Assert.IsType<NotOnCall>(result);
    }

    [Fact]
    public void Evaluate_PresenceTakesPrecedenceOverMic_MicNeverOverridesAKnownMeetingEnd()
    {
        var presence = new GraphPresenceSnapshot(Availability: "Busy", Activity: "InACall");
        var meetingEnd = DateTimeOffset.UtcNow.AddMinutes(5);

        var result = OnCallStateEvaluator.Evaluate(presence, currentMeetingEndUtc: meetingEnd, microphoneActive: true);

        var until = Assert.IsType<OnCallUntil>(result);
        Assert.Equal(meetingEnd, until.EndUtc);
    }
}
