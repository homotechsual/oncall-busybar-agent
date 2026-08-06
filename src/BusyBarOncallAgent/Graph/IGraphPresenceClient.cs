namespace BusyBarOncallAgent.Graph;

public interface IGraphPresenceClient
{
    Task<GraphPresenceSnapshot> GetPresenceAsync(CancellationToken cancellationToken);

    /// <summary>The soonest-ending calendar event currently overlapping now, or null if there is
    /// none. Only meaningful to call when presence already indicates a call; a null result there
    /// means an ad hoc call with no matching invite.</summary>
    Task<DateTimeOffset?> GetCurrentMeetingEndAsync(CancellationToken cancellationToken);
}
