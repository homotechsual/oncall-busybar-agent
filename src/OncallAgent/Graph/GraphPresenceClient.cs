using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace OncallAgent.Graph;

public sealed class GraphPresenceClient : IGraphPresenceClient
{
    private readonly HttpClient _http;
    private readonly IGraphAuthService _authService;

    public GraphPresenceClient(HttpClient http, IGraphAuthService authService)
    {
        _http = http;
        _authService = authService;
    }

    public async Task<GraphPresenceSnapshot> GetPresenceAsync(CancellationToken cancellationToken)
    {
        var token = await _authService.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Get, "v1.0/me/presence");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GraphPresenceResponse>(cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Graph presence response was empty.");

        return new GraphPresenceSnapshot(body.Availability, body.Activity);
    }

    /// <summary>Queries a one-minute window starting now, in UTC (the "Prefer: outlook.timezone"
    /// header pins the returned times to UTC regardless of the mailbox's own time zone setting,
    /// per Microsoft Graph's documented calendarView behavior), and returns the soonest-ending of
    /// any events that overlap that window.</summary>
    public async Task<DateTimeOffset?> GetCurrentMeetingEndAsync(CancellationToken cancellationToken)
    {
        var token = await _authService.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        var startDateTime = Uri.EscapeDataString(now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
        var endDateTime = Uri.EscapeDataString(now.AddMinutes(1).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));

        using var request = new HttpRequestMessage(HttpMethod.Get, $"v1.0/me/calendarView?startDateTime={startDateTime}&endDateTime={endDateTime}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Prefer", "outlook.timezone=\"UTC\"");

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GraphCalendarViewResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);

        if (body is null || body.Value.Count == 0)
        {
            return null;
        }

        return body.Value
            .Select(e => new DateTimeOffset(DateTime.SpecifyKind(
                DateTime.Parse(e.End.DateTime, CultureInfo.InvariantCulture, DateTimeStyles.None), DateTimeKind.Utc)))
            .OrderBy(end => end)
            .First();
    }

    private sealed record GraphPresenceResponse(
        [property: JsonPropertyName("availability")] string Availability,
        [property: JsonPropertyName("activity")] string Activity);

    private sealed record GraphCalendarViewResponse(
        [property: JsonPropertyName("value")] List<GraphCalendarEvent> Value);

    private sealed record GraphCalendarEvent(
        [property: JsonPropertyName("end")] GraphDateTimeTimeZone End);

    private sealed record GraphDateTimeTimeZone(
        [property: JsonPropertyName("dateTime")] string DateTime);
}
