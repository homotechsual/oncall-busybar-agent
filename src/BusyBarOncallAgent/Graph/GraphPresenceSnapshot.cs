namespace BusyBarOncallAgent.Graph;

/// <summary>A point-in-time read of the signed in user's Microsoft Graph presence.</summary>
public sealed record GraphPresenceSnapshot(string Availability, string Activity);
