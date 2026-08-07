using System.Net;
using System.Net.Http.Headers;
using OncallAgent.Graph;
using OncallAgent.Tests.Internal;
using Xunit;

namespace OncallAgent.Tests.Graph;

file sealed class FakeGraphAuthService : IGraphAuthService
{
    public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult("fake-token");
    public Task SignOutAsync() => Task.CompletedTask;
}

public class GraphPresenceClientTests
{
    private static (GraphPresenceClient client, FakeHttpMessageHandler handler) CreateClient()
    {
        var handler = new FakeHttpMessageHandler { ResponseBody = "{\"availability\":\"Busy\",\"activity\":\"InACall\"}" };
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/") };
        return (new GraphPresenceClient(http, new FakeGraphAuthService()), handler);
    }

    [Fact]
    public async Task GetPresenceAsync_ParsesAvailabilityAndActivity()
    {
        var (client, handler) = CreateClient();

        var result = await client.GetPresenceAsync(CancellationToken.None);

        Assert.Equal("Busy", result.Availability);
        Assert.Equal("InACall", result.Activity);
        Assert.Equal("v1.0/me/presence", handler.LastRequest!.RequestUri!.PathAndQuery.TrimStart('/'));
    }

    [Fact]
    public async Task GetPresenceAsync_SendsBearerToken()
    {
        var (client, handler) = CreateClient();

        await client.GetPresenceAsync(CancellationToken.None);

        Assert.Equal(new AuthenticationHeaderValue("Bearer", "fake-token"), handler.LastRequest!.Headers.Authorization);
    }

    [Fact]
    public async Task GetCurrentMeetingEndAsync_ReturnsNull_WhenNoEventsOverlapNow()
    {
        var handler = new FakeHttpMessageHandler { ResponseBody = "{\"value\":[]}" };
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/") };
        var client = new GraphPresenceClient(http, new FakeGraphAuthService());

        var result = await client.GetCurrentMeetingEndAsync(CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCurrentMeetingEndAsync_ReturnsSoonestEndingEvent_WhenSeveralOverlapNow()
    {
        var handler = new FakeHttpMessageHandler
        {
            ResponseBody = """
            {"value":[
                {"end":{"dateTime":"2026-08-05T15:30:00.0000000","timeZone":"UTC"}},
                {"end":{"dateTime":"2026-08-05T15:10:00.0000000","timeZone":"UTC"}}
            ]}
            """
        };
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/") };
        var client = new GraphPresenceClient(http, new FakeGraphAuthService());

        var result = await client.GetCurrentMeetingEndAsync(CancellationToken.None);

        Assert.Equal(new DateTimeOffset(2026, 8, 5, 15, 10, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public async Task GetCurrentMeetingEndAsync_RequestsUtcTimeZone()
    {
        var handler = new FakeHttpMessageHandler { ResponseBody = "{\"value\":[]}" };
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/") };
        var client = new GraphPresenceClient(http, new FakeGraphAuthService());

        await client.GetCurrentMeetingEndAsync(CancellationToken.None);

        Assert.Equal("outlook.timezone=\"UTC\"", handler.LastRequest!.Headers.GetValues("Prefer").Single());
    }
}
