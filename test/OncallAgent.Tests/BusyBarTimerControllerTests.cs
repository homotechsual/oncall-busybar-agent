using OncallAgent.OnCall;
using OncallAgent.Tests.Internal;
using Xunit;

namespace OncallAgent.Tests;

public class BusyBarTimerControllerTests
{
    private static (BusyBarTimerController controller, FakeHttpMessageHandler handler) CreateController()
    {
        var handler = new FakeHttpMessageHandler { ResponseBody = "{\"result\":\"OK\"}" };
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://10.0.4.20/") };
        var bar = new Busy.Bar.BusyBar(http, new Busy.Bar.BusyBarOptions());
        return (new BusyBarTimerController(bar), handler);
    }

    [Fact]
    public async Task ApplyAsync_SendsNoRequest_WhenStateIsNotOnCall_AndNothingWasEverApplied()
    {
        var (controller, handler) = CreateController();

        await controller.ApplyAsync(new NotOnCall(), CancellationToken.None);

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ApplyAsync_StartsAnInfiniteTimer_WhenStateIsOnCallIndefinite()
    {
        var (controller, handler) = CreateController();

        await controller.ApplyAsync(new OnCallIndefinite(), CancellationToken.None);

        Assert.Single(handler.Requests);
        Assert.Contains("\"type\":\"INFINITE\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task ApplyAsync_SendsNoSecondRequest_WhenTheSameIndefiniteStateIsAppliedAgain()
    {
        var (controller, handler) = CreateController();

        await controller.ApplyAsync(new OnCallIndefinite(), CancellationToken.None);
        await controller.ApplyAsync(new OnCallIndefinite(), CancellationToken.None);

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ApplyAsync_StartsATimedTimer_WhenStateIsOnCallUntil()
    {
        var (controller, handler) = CreateController();
        var end = DateTimeOffset.UtcNow.AddMinutes(20);

        await controller.ApplyAsync(new OnCallUntil(end), CancellationToken.None);

        Assert.Single(handler.Requests);
        Assert.Contains("\"type\":\"SIMPLE\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task ApplyAsync_SendsNoSecondRequest_WhenTheMeetingEndMovesByLessThanTheThreshold()
    {
        var (controller, handler) = CreateController();
        var end = DateTimeOffset.UtcNow.AddMinutes(20);

        await controller.ApplyAsync(new OnCallUntil(end), CancellationToken.None);
        await controller.ApplyAsync(new OnCallUntil(end.AddSeconds(30)), CancellationToken.None);

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ApplyAsync_SendsASecondRequest_WhenTheMeetingEndMovesByMoreThanTheThreshold()
    {
        var (controller, handler) = CreateController();
        var end = DateTimeOffset.UtcNow.AddMinutes(20);

        await controller.ApplyAsync(new OnCallUntil(end), CancellationToken.None);
        await controller.ApplyAsync(new OnCallUntil(end.AddSeconds(90)), CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task ApplyAsync_StopsTheTimer_WhenTransitioningFromOnCallToNotOnCall()
    {
        var (controller, handler) = CreateController();

        await controller.ApplyAsync(new OnCallIndefinite(), CancellationToken.None);
        await controller.ApplyAsync(new NotOnCall(), CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("\"type\":\"NOT_STARTED\"", handler.LastRequestBody);
    }
}
