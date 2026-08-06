# busybar-oncall-agent Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Windows tray app that keeps a physical BUSY Bar's native Busy timer in sync with real Microsoft Teams on-call status, sized to the current meeting's remaining time when known.

**Architecture:** Two polling timers (Graph presence/calendar, local microphone activity) feed a pure `OnCallStateEvaluator`, whose output a `BusyBarTimerController` turns into BUSY Bar API calls, tracking what it last applied so it does not restart the device's timer needlessly. A `NotifyIcon`-based tray shell with no main window hosts the polling timers and a right-click menu.

**Tech Stack:** .NET 10 (`net10.0-windows`, WinForms for the tray icon), `BusyBar` NuGet package, `Microsoft.Identity.Client` (MSAL.NET) for Graph auth, `Microsoft.Extensions.Hosting` for DI/config/logging.

## Global Constraints

- Solution/project naming mirrors `psatool-busybar-agent`: solution `BusyBarOncallAgent.sln`, main project `src/BusyBarOncallAgent/BusyBarOncallAgent.csproj`, test project `test/BusyBarOncallAgent.Tests/BusyBarOncallAgent.Tests.csproj`.
- Target framework: `net10.0-windows`. Main project uses plain `Microsoft.NET.Sdk` (not `Microsoft.NET.Sdk.Worker` — that SDK is for headless `BackgroundService` hosting; this app needs a WinForms message loop) with `<UseWindowsForms>true</UseWindowsForms>`.
- Package versions (confirmed current on nuget.org as of this plan): `Microsoft.Extensions.Hosting` `10.0.0`, `Microsoft.Extensions.Http` `10.0.0`, `Microsoft.Extensions.Options.DataAnnotations` `10.0.0`, `BusyBar` `1.0.0` (all matching `psatool-busybar-agent`'s exact versions), `Microsoft.Identity.Client` `4.87.0`, `Microsoft.Identity.Client.Broker` `4.87.0`, `Microsoft.Identity.Client.Extensions.Msal` `4.87.0`.
- Test project package versions match `psatool-busybar-agent`'s exactly: `Microsoft.NET.Test.Sdk` `18.8.1`, `xunit` `2.9.3`, `xunit.runner.visualstudio` `3.1.5`.
- Config keys: `Graph:ClientId`, `Graph:TenantId` (default `organizations`), `Graph:PollIntervalSeconds` (default `30`), `Mic:Enabled` (default `true`), `Mic:PollIntervalSeconds` (default `5`), `BusyBar:Address` (default `10.0.4.20`).
- The mic detector's registry key layout (`HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone\NonPackaged`, with `LastUsedTimeStart`/`LastUsedTimeStop` `REG_QWORD` `FILETIME` values per app subkey) is documented by the community, not an official Microsoft API contract. Verify it against a real Windows machine at implementation time; do not treat the plan's description as ground truth if the real registry disagrees.
- The BUSY Bar's `Busy` API's exact snapshot payload shape for *starting* a fresh timer (in particular, what `CardId` value is expected/accepted) is not confirmed against a real device. This plan uses the literal string `"oncall"` as a caller-chosen label, pending that confirmation — flag this explicitly when verifying against real hardware.
- No em dashes ("—") in the README or any doc content written for this project (matches the convention already established for `psatool-busybar-agent`'s docs).
- This environment's Bash tool has an intermittent Cygwin fork issue (`cygheap read copy failed`). If a `dotnet`/`git` command via Bash fails with that error, retry the same command using the PowerShell tool instead.
- Repo root for all paths below: `J:\Projects\Busy\busybar-oncall-agent` (already `git init`-ed, empty).

---

### Task 1: Solution and project scaffolding

**Files:**
- Create: `BusyBarOncallAgent.sln`
- Create: `src/BusyBarOncallAgent/BusyBarOncallAgent.csproj`
- Create: `src/BusyBarOncallAgent/Program.cs`
- Create: `src/BusyBarOncallAgent/appsettings.json`
- Create: `test/BusyBarOncallAgent.Tests/BusyBarOncallAgent.Tests.csproj`
- Create: `test/BusyBarOncallAgent.Tests/Internal/FakeHttpMessageHandler.cs`
- Create: `.gitignore`
- Create: `LICENSE`
- Create: `README.md`

**Interfaces:**
- Produces: a buildable, runnable solution with an empty main project and a test project that can reference it, plus the shared `FakeHttpMessageHandler` test helper every later HTTP-facing test task uses.

- [ ] **Step 1: Create the solution and projects via the .NET CLI**

Run (PowerShell, from the repo root):

```powershell
dotnet new sln -n BusyBarOncallAgent
dotnet new console -n BusyBarOncallAgent -o src/BusyBarOncallAgent
dotnet new xunit -n BusyBarOncallAgent.Tests -o test/BusyBarOncallAgent.Tests
dotnet sln add src/BusyBarOncallAgent/BusyBarOncallAgent.csproj
dotnet sln add test/BusyBarOncallAgent.Tests/BusyBarOncallAgent.Tests.csproj
dotnet add test/BusyBarOncallAgent.Tests/BusyBarOncallAgent.Tests.csproj reference src/BusyBarOncallAgent/BusyBarOncallAgent.csproj
```

Expected: `BusyBarOncallAgent.sln` exists at the repo root and references both projects; `dotnet new xunit` already added `Microsoft.NET.Test.Sdk`, `xunit`, and `xunit.runner.visualstudio` to the test project (their versions may differ from this plan's Global Constraints; Step 3 pins them to the exact versions used).

- [ ] **Step 2: Replace `src/BusyBarOncallAgent/BusyBarOncallAgent.csproj` with**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>BusyBarOncallAgent</RootNamespace>
    <UserSecretsId>busybar-oncall-agent</UserSecretsId>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.Options.DataAnnotations" Version="10.0.0" />
    <PackageReference Include="BusyBar" Version="1.0.0" />
    <PackageReference Include="Microsoft.Identity.Client" Version="4.87.0" />
    <PackageReference Include="Microsoft.Identity.Client.Broker" Version="4.87.0" />
    <PackageReference Include="Microsoft.Identity.Client.Extensions.Msal" Version="4.87.0" />
  </ItemGroup>

  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

  <ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
      <_Parameter1>BusyBarOncallAgent.Tests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>

</Project>
```

Note: `Microsoft.NET.Sdk` (not `Microsoft.NET.Sdk.Worker`) does not auto-copy `appsettings.json` to the output directory the way the Worker SDK does; the explicit `<None Update>` block above is required or the app will not find its config at runtime.

- [ ] **Step 3: Replace `test/BusyBarOncallAgent.Tests/BusyBarOncallAgent.Tests.csproj` with**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.8.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\BusyBarOncallAgent\BusyBarOncallAgent.csproj" />
  </ItemGroup>

</Project>
```

The test project's target framework must also be `net10.0-windows` (not plain `net10.0`) since it references a project that requires the Windows-specific TFM.

- [ ] **Step 4: Delete the template files `dotnet new` generated that this plan replaces**

`dotnet new console` generates a `Program.cs` this plan overwrites in Step 5; `dotnet new xunit` generates a `UnitTest1.cs` this plan does not need. Delete `test/BusyBarOncallAgent.Tests/UnitTest1.cs`.

- [ ] **Step 5: Replace `src/BusyBarOncallAgent/Program.cs` with**

```csharp
Console.WriteLine("BusyBarOncallAgent starting...");
```

A minimal but real, compiling entry point, replaced with the full tray application in Task 7.

- [ ] **Step 6: Create `src/BusyBarOncallAgent/appsettings.json`**

```json
{
  "Graph": {
    "ClientId": "",
    "TenantId": "organizations",
    "PollIntervalSeconds": 30
  },
  "Mic": {
    "Enabled": true,
    "PollIntervalSeconds": 5
  },
  "BusyBar": {
    "Address": "10.0.4.20"
  }
}
```

- [ ] **Step 7: Create `test/BusyBarOncallAgent.Tests/Internal/FakeHttpMessageHandler.cs`**

```csharp
using System.Net;
using System.Text;

namespace BusyBarOncallAgent.Tests.Internal;

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = new();
    public HttpRequestMessage? LastRequest => Requests.Count > 0 ? Requests[^1] : null;
    public string? LastRequestBody { get; private set; }

    /// <summary>Every request body seen so far, in order — unlike <see cref="LastRequestBody"/>,
    /// lets a test verify content varied across several requests.</summary>
    public List<string?> RequestBodies { get; } = new();

    /// <summary>Status code and body returned for every request, unless <see cref="Respond"/> is set.</summary>
    public HttpStatusCode ResponseStatusCode { get; set; } = HttpStatusCode.OK;
    public string ResponseBody { get; set; } = "{}";

    /// <summary>When set, overrides <see cref="ResponseStatusCode"/>/<see cref="ResponseBody"/> so a
    /// test can return different responses for different requests.</summary>
    public Func<HttpRequestMessage, HttpResponseMessage>? Respond { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        RequestBodies.Add(LastRequestBody);

        if (Respond is not null)
        {
            return Respond(request);
        }

        return new HttpResponseMessage(ResponseStatusCode)
        {
            Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json")
        };
    }
}
```

- [ ] **Step 8: Create `.gitignore`**

```
.superpowers/
bin/
obj/
artifacts/
*.user
.vs/
.env
```

- [ ] **Step 9: Create `LICENSE`**

```
MIT License

Copyright (c) 2026 MJCO

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

- [ ] **Step 10: Create a minimal `README.md`**

```markdown
# busybar-oncall-agent

A Windows tray app that keeps a physical BUSY Bar's built-in timer in sync with real Microsoft
Teams on-call status.

Full setup instructions land in a later commit.
```

Task 7 replaces this with the complete version once every piece exists.

- [ ] **Step 11: Build and verify**

Run (PowerShell, from the repo root):

```powershell
dotnet build BusyBarOncallAgent.sln
```

Expected: build succeeds with 0 errors.

- [ ] **Step 12: Commit**

```bash
git add -A
git commit -m "Add solution and project scaffolding"
```

---

### Task 2: OnCallState and OnCallStateEvaluator

**Files:**
- Create: `src/BusyBarOncallAgent/Graph/GraphPresenceSnapshot.cs`
- Create: `src/BusyBarOncallAgent/OnCall/OnCallState.cs`
- Create: `src/BusyBarOncallAgent/OnCall/OnCallStateEvaluator.cs`
- Test: `test/BusyBarOncallAgent.Tests/OnCall/OnCallStateEvaluatorTests.cs`

**Interfaces:**
- Produces: `GraphPresenceSnapshot(string Availability, string Activity)`; `OnCallState` (abstract record) with cases `NotOnCall`, `OnCallUntil(DateTimeOffset EndUtc)`, `OnCallIndefinite`; `OnCallStateEvaluator.Evaluate(GraphPresenceSnapshot? presence, DateTimeOffset? currentMeetingEndUtc, bool microphoneActive) -> OnCallState`. Every later task that produces an `OnCallState` or consumes one uses these exact names.

- [ ] **Step 1: Create `src/BusyBarOncallAgent/Graph/GraphPresenceSnapshot.cs`**

```csharp
namespace BusyBarOncallAgent.Graph;

/// <summary>A point-in-time read of the signed in user's Microsoft Graph presence.</summary>
public sealed record GraphPresenceSnapshot(string Availability, string Activity);
```

- [ ] **Step 2: Create `src/BusyBarOncallAgent/OnCall/OnCallState.cs`**

```csharp
namespace BusyBarOncallAgent.OnCall;

/// <summary>What the BUSY Bar's timer should currently show, as decided by
/// <see cref="OnCallStateEvaluator"/>.</summary>
public abstract record OnCallState;

/// <summary>Not on a call; the device's timer should be stopped.</summary>
public sealed record NotOnCall : OnCallState;

/// <summary>On a call with a known end time (from a matching calendar event) — the timer should
/// be sized to the remaining time until then.</summary>
public sealed record OnCallUntil(DateTimeOffset EndUtc) : OnCallState;

/// <summary>On a call with no known end time (an ad hoc call, or the local microphone fallback,
/// neither of which carries a duration) — the timer should run open ended.</summary>
public sealed record OnCallIndefinite : OnCallState;
```

- [ ] **Step 3: Write the failing tests in `test/BusyBarOncallAgent.Tests/OnCall/OnCallStateEvaluatorTests.cs`**

```csharp
using BusyBarOncallAgent.Graph;
using BusyBarOncallAgent.OnCall;
using Xunit;

namespace BusyBarOncallAgent.Tests.OnCall;

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
```

- [ ] **Step 4: Run the tests to verify they fail**

Run (PowerShell, from the repo root):

```powershell
dotnet test test/BusyBarOncallAgent.Tests/BusyBarOncallAgent.Tests.csproj --filter OnCallStateEvaluatorTests
```

Expected: build FAILS — `OnCallStateEvaluator` does not exist yet.

- [ ] **Step 5: Create `src/BusyBarOncallAgent/OnCall/OnCallStateEvaluator.cs`**

```csharp
using BusyBarOncallAgent.Graph;

namespace BusyBarOncallAgent.OnCall;

/// <summary>Pure function turning the raw signals into an <see cref="OnCallState"/>. No I/O.
/// Microsoft Graph presence is the primary signal; the microphone is consulted only when
/// presence itself does not already indicate a call — a fallback for what presence misses, never
/// a second independent trigger, and it never carries a known end time.</summary>
public static class OnCallStateEvaluator
{
    private static readonly string[] OnCallActivities = ["InACall", "InAMeeting", "Presenting"];

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

        return new NotOnCall();
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run (PowerShell, from the repo root):

```powershell
dotnet test test/BusyBarOncallAgent.Tests/BusyBarOncallAgent.Tests.csproj --filter OnCallStateEvaluatorTests
```

Expected: all 9 tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/BusyBarOncallAgent/Graph/GraphPresenceSnapshot.cs src/BusyBarOncallAgent/OnCall/ test/BusyBarOncallAgent.Tests/OnCall/
git commit -m "Add OnCallState and the pure OnCallStateEvaluator"
```

---

### Task 3: Graph auth contract and GraphPresenceClient

**Files:**
- Create: `src/BusyBarOncallAgent/Graph/IGraphAuthService.cs`
- Create: `src/BusyBarOncallAgent/Graph/IGraphPresenceClient.cs`
- Create: `src/BusyBarOncallAgent/Graph/GraphPresenceClient.cs`
- Test: `test/BusyBarOncallAgent.Tests/Graph/GraphPresenceClientTests.cs`

**Interfaces:**
- Consumes: `GraphPresenceSnapshot` from Task 2.
- Produces: `IGraphAuthService` with `Task<string> GetAccessTokenAsync(CancellationToken)` and
  `Task SignOutAsync()` — Task 6's `GraphAuthService` implements this. `IGraphPresenceClient` with
  `Task<GraphPresenceSnapshot> GetPresenceAsync(CancellationToken)` and
  `Task<DateTimeOffset?> GetCurrentMeetingEndAsync(CancellationToken)` — Task 7's DI wiring
  registers `GraphPresenceClient` against this interface.

- [ ] **Step 1: Create `src/BusyBarOncallAgent/Graph/IGraphAuthService.cs`**

```csharp
namespace BusyBarOncallAgent.Graph;

/// <summary>Supplies bearer tokens for Microsoft Graph calls. Implemented by
/// <c>GraphAuthService</c> (MSAL.NET, interactive sign in with a cached, silently refreshed
/// token); this interface exists so callers do not depend on MSAL directly.</summary>
public interface IGraphAuthService
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
    Task SignOutAsync();
}
```

- [ ] **Step 2: Create `src/BusyBarOncallAgent/Graph/IGraphPresenceClient.cs`**

```csharp
namespace BusyBarOncallAgent.Graph;

public interface IGraphPresenceClient
{
    Task<GraphPresenceSnapshot> GetPresenceAsync(CancellationToken cancellationToken);

    /// <summary>The soonest-ending calendar event currently overlapping now, or null if there is
    /// none. Only meaningful to call when presence already indicates a call; a null result there
    /// means an ad hoc call with no matching invite.</summary>
    Task<DateTimeOffset?> GetCurrentMeetingEndAsync(CancellationToken cancellationToken);
}
```

- [ ] **Step 3: Write the failing tests in `test/BusyBarOncallAgent.Tests/Graph/GraphPresenceClientTests.cs`**

```csharp
using System.Net;
using System.Net.Http.Headers;
using BusyBarOncallAgent.Graph;
using BusyBarOncallAgent.Tests.Internal;
using Xunit;

namespace BusyBarOncallAgent.Tests.Graph;

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
```

- [ ] **Step 4: Run the tests to verify they fail**

Run (PowerShell, from the repo root):

```powershell
dotnet test test/BusyBarOncallAgent.Tests/BusyBarOncallAgent.Tests.csproj --filter GraphPresenceClientTests
```

Expected: build FAILS — `GraphPresenceClient` does not exist yet.

- [ ] **Step 5: Create `src/BusyBarOncallAgent/Graph/GraphPresenceClient.cs`**

```csharp
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BusyBarOncallAgent.Graph;

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
```

- [ ] **Step 6: Run the tests to verify they pass**

Run (PowerShell, from the repo root):

```powershell
dotnet test test/BusyBarOncallAgent.Tests/BusyBarOncallAgent.Tests.csproj --filter GraphPresenceClientTests
```

Expected: all 5 tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/BusyBarOncallAgent/Graph/ test/BusyBarOncallAgent.Tests/Graph/
git commit -m "Add IGraphAuthService, IGraphPresenceClient, and GraphPresenceClient"
```

---

### Task 4: MicrophoneActivityDetector

**Files:**
- Create: `src/BusyBarOncallAgent/Mic/MicrophoneAppUsage.cs`
- Create: `src/BusyBarOncallAgent/Mic/IMicrophoneRegistryReader.cs`
- Create: `src/BusyBarOncallAgent/Mic/Win32MicrophoneRegistryReader.cs`
- Create: `src/BusyBarOncallAgent/Mic/MicrophoneActivityDetector.cs`
- Test: `test/BusyBarOncallAgent.Tests/Mic/MicrophoneActivityDetectorTests.cs`

**Interfaces:**
- Produces: `MicrophoneActivityDetector.IsActive() -> bool`, consumed by Task 7's polling. The
  pure decision logic (`MicrophoneActivityDetector.IsActive(IReadOnlyList<MicrophoneAppUsage>)`,
  `internal`) is what Task 7's DI wiring registers `MicrophoneActivityDetector` as a singleton
  around, but only the instance method matters to other tasks.

- [ ] **Step 1: Create `src/BusyBarOncallAgent/Mic/MicrophoneAppUsage.cs`**

```csharp
namespace BusyBarOncallAgent.Mic;

/// <summary>One application's recorded microphone access, as read from the registry. Null start
/// or stop times mean that value was never recorded (the app has never used the microphone, or
/// Windows has not written a stop time yet).</summary>
public sealed record MicrophoneAppUsage(string AppId, DateTimeOffset? LastUsedStartUtc, DateTimeOffset? LastUsedStopUtc);
```

- [ ] **Step 2: Create `src/BusyBarOncallAgent/Mic/IMicrophoneRegistryReader.cs`**

```csharp
namespace BusyBarOncallAgent.Mic;

public interface IMicrophoneRegistryReader
{
    IReadOnlyList<MicrophoneAppUsage> ReadAppUsages();
}
```

- [ ] **Step 3: Write the failing tests in `test/BusyBarOncallAgent.Tests/Mic/MicrophoneActivityDetectorTests.cs`**

```csharp
using BusyBarOncallAgent.Mic;
using Xunit;

namespace BusyBarOncallAgent.Tests.Mic;

public class MicrophoneActivityDetectorTests
{
    [Fact]
    public void IsActive_ReturnsFalse_WhenNoUsagesRecorded()
    {
        var result = MicrophoneActivityDetector.IsActive([]);

        Assert.False(result);
    }

    [Fact]
    public void IsActive_ReturnsTrue_WhenAnAppHasNeverStopped()
    {
        var usages = new[]
        {
            new MicrophoneAppUsage("Teams", DateTimeOffset.UtcNow.AddMinutes(-5), LastUsedStopUtc: null)
        };

        var result = MicrophoneActivityDetector.IsActive(usages);

        Assert.True(result);
    }

    [Fact]
    public void IsActive_ReturnsTrue_WhenTheMostRecentStartIsAfterTheMostRecentStop()
    {
        var usages = new[]
        {
            new MicrophoneAppUsage("Teams", DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(-10))
        };

        var result = MicrophoneActivityDetector.IsActive(usages);

        Assert.True(result);
    }

    [Fact]
    public void IsActive_ReturnsFalse_WhenTheAppHasStoppedSinceItLastStarted()
    {
        var usages = new[]
        {
            new MicrophoneAppUsage("Teams", DateTimeOffset.UtcNow.AddMinutes(-10), DateTimeOffset.UtcNow.AddMinutes(-1))
        };

        var result = MicrophoneActivityDetector.IsActive(usages);

        Assert.False(result);
    }

    [Fact]
    public void IsActive_ReturnsTrue_WhenAnyOneOfSeveralAppsIsActive()
    {
        var usages = new[]
        {
            new MicrophoneAppUsage("Discord", DateTimeOffset.UtcNow.AddMinutes(-10), DateTimeOffset.UtcNow.AddMinutes(-1)),
            new MicrophoneAppUsage("Teams", DateTimeOffset.UtcNow.AddMinutes(-2), LastUsedStopUtc: null)
        };

        var result = MicrophoneActivityDetector.IsActive(usages);

        Assert.True(result);
    }

    [Fact]
    public void IsActive_ReturnsFalse_WhenAnAppHasNeverStarted()
    {
        var usages = new[]
        {
            new MicrophoneAppUsage("Teams", LastUsedStartUtc: null, LastUsedStopUtc: null)
        };

        var result = MicrophoneActivityDetector.IsActive(usages);

        Assert.False(result);
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

Run (PowerShell, from the repo root):

```powershell
dotnet test test/BusyBarOncallAgent.Tests/BusyBarOncallAgent.Tests.csproj --filter MicrophoneActivityDetectorTests
```

Expected: build FAILS — `MicrophoneActivityDetector` does not exist yet.

- [ ] **Step 5: Create `src/BusyBarOncallAgent/Mic/MicrophoneActivityDetector.cs`**

```csharp
namespace BusyBarOncallAgent.Mic;

public sealed class MicrophoneActivityDetector
{
    private readonly IMicrophoneRegistryReader _reader;

    public MicrophoneActivityDetector(IMicrophoneRegistryReader reader)
    {
        _reader = reader;
    }

    public bool IsActive() => IsActive(_reader.ReadAppUsages());

    /// <summary>Pure decision logic, separated from the registry read so it is unit testable
    /// without touching the real registry. An app is actively using the microphone when its
    /// last-used-stop time is absent (never stopped) or earlier than its last-used-start time (a
    /// new session started since the last recorded stop). Internal (not private) so tests can
    /// drive it directly, matching PollingBackgroundService's own pattern in
    /// psatool-busybar-agent.</summary>
    internal static bool IsActive(IReadOnlyList<MicrophoneAppUsage> usages) =>
        usages.Any(u => u.LastUsedStartUtc is not null &&
            (u.LastUsedStopUtc is null || u.LastUsedStopUtc < u.LastUsedStartUtc));
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run (PowerShell, from the repo root):

```powershell
dotnet test test/BusyBarOncallAgent.Tests/BusyBarOncallAgent.Tests.csproj --filter MicrophoneActivityDetectorTests
```

Expected: all 6 tests pass.

- [ ] **Step 7: Create `src/BusyBarOncallAgent/Mic/Win32MicrophoneRegistryReader.cs`**

Not unit tested (real registry I/O); verify against a real Windows machine at implementation
time, per this plan's Global Constraints.

```csharp
using Microsoft.Win32;

namespace BusyBarOncallAgent.Mic;

/// <summary>Reads Windows' own record of which applications have used the microphone, from the
/// registry key that backs the OS's microphone privacy indicator.</summary>
public sealed class Win32MicrophoneRegistryReader : IMicrophoneRegistryReader
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone\NonPackaged";

    public IReadOnlyList<MicrophoneAppUsage> ReadAppUsages()
    {
        using var root = Registry.CurrentUser.OpenSubKey(KeyPath);
        if (root is null)
        {
            return [];
        }

        var usages = new List<MicrophoneAppUsage>();
        foreach (var appId in root.GetSubKeyNames())
        {
            using var appKey = root.OpenSubKey(appId);
            if (appKey is null)
            {
                continue;
            }

            usages.Add(new MicrophoneAppUsage(
                appId,
                ReadFileTime(appKey, "LastUsedTimeStart"),
                ReadFileTime(appKey, "LastUsedTimeStop")));
        }

        return usages;
    }

    private static DateTimeOffset? ReadFileTime(RegistryKey key, string valueName)
    {
        if (key.GetValue(valueName) is not long fileTime || fileTime == 0)
        {
            return null;
        }

        return DateTimeOffset.FromFileTime(fileTime);
    }
}
```

- [ ] **Step 8: Build and manually verify against the real registry**

Run (PowerShell):

```powershell
dotnet build BusyBarOncallAgent.sln
```

Expected: build succeeds. Separately, open Registry Editor and navigate to
`HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone\NonPackaged`
on the actual development machine to confirm the key exists and its subkeys carry
`LastUsedTimeStart`/`LastUsedTimeStop` `REG_QWORD` values, matching this plan's Global
Constraints note that this layout is community documented, not an official contract. If it
disagrees with what this task implemented, fix `Win32MicrophoneRegistryReader` to match reality
before continuing, and note the discrepancy in the commit message.

- [ ] **Step 9: Commit**

```bash
git add src/BusyBarOncallAgent/Mic/ test/BusyBarOncallAgent.Tests/Mic/
git commit -m "Add MicrophoneActivityDetector with a real Windows registry reader"
```

---

### Task 5: BusyBarTimerController

**Files:**
- Create: `src/BusyBarOncallAgent/BusyBarTimerController.cs`
- Test: `test/BusyBarOncallAgent.Tests/BusyBarTimerControllerTests.cs`

**Interfaces:**
- Consumes: `OnCallState` and its three cases from Task 2; `Busy.Bar.BusyBar` and its `Busy`
  namespace types (`BusySnapshot`, `BusySnapshotState`, `BusySnapshotNotStarted`,
  `BusySnapshotInfinite`, `BusySnapshotSimple`) from the `BusyBar` NuGet package.
- Produces: `BusyBarTimerController.ApplyAsync(OnCallState state, CancellationToken) -> Task`,
  consumed by Task 7's polling.

- [ ] **Step 1: Write the failing tests in `test/BusyBarOncallAgent.Tests/BusyBarTimerControllerTests.cs`**

```csharp
using BusyBarOncallAgent.OnCall;
using BusyBarOncallAgent.Tests.Internal;
using Xunit;

namespace BusyBarOncallAgent.Tests;

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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run (PowerShell, from the repo root):

```powershell
dotnet test test/BusyBarOncallAgent.Tests/BusyBarOncallAgent.Tests.csproj --filter BusyBarTimerControllerTests
```

Expected: build FAILS — `BusyBarTimerController` does not exist yet.

- [ ] **Step 3: Create `src/BusyBarOncallAgent/BusyBarTimerController.cs`**

```csharp
using Busy.Bar;
using BusyBarOncallAgent.OnCall;

namespace BusyBarOncallAgent;

/// <summary>Turns an <see cref="OnCallState"/> into BUSY timer API calls, tracking the last
/// state actually applied so it does not restart the device's timer (and its animation) on every
/// poll while nothing has meaningfully changed. The CardId value used to start a fresh timer is
/// not confirmed against a real device; see this plan's Global Constraints.</summary>
public sealed class BusyBarTimerController
{
    private const string CardId = "oncall";
    private static readonly TimeSpan ReapplyThreshold = TimeSpan.FromSeconds(60);

    private readonly BusyBar _bar;
    private OnCallState _lastApplied = new NotOnCall();

    public BusyBarTimerController(BusyBar bar)
    {
        _bar = bar;
    }

    public async Task ApplyAsync(OnCallState state, CancellationToken cancellationToken)
    {
        if (!NeedsReapply(_lastApplied, state))
        {
            return;
        }

        var snapshotState = ToSnapshotState(state);
        var snapshot = new BusySnapshot(snapshotState, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        await _bar.BusySnapshotSetAsync(snapshot, cancellationToken: cancellationToken).ConfigureAwait(false);
        _lastApplied = state;
    }

    internal static bool NeedsReapply(OnCallState previous, OnCallState next) => (previous, next) switch
    {
        (NotOnCall, NotOnCall) => false,
        (OnCallIndefinite, OnCallIndefinite) => false,
        (OnCallUntil prev, OnCallUntil nxt) => (nxt.EndUtc - prev.EndUtc).Duration() > ReapplyThreshold,
        _ => true
    };

    private static BusySnapshotState ToSnapshotState(OnCallState state) => state switch
    {
        NotOnCall => new BusySnapshotNotStarted(),
        OnCallIndefinite => new BusySnapshotInfinite { CardId = CardId, IsPaused = false },
        OnCallUntil until => new BusySnapshotSimple
        {
            CardId = CardId,
            TimeLeftMs = Math.Max(0, (long)(until.EndUtc - DateTimeOffset.UtcNow).TotalMilliseconds),
            IsPaused = false
        },
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run (PowerShell, from the repo root):

```powershell
dotnet test test/BusyBarOncallAgent.Tests/BusyBarOncallAgent.Tests.csproj --filter BusyBarTimerControllerTests
```

Expected: all 7 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/BusyBarOncallAgent/BusyBarTimerController.cs test/BusyBarOncallAgent.Tests/BusyBarTimerControllerTests.cs
git commit -m "Add BusyBarTimerController"
```

---

### Task 6: GraphAuthService (MSAL.NET)

**Files:**
- Create: `src/BusyBarOncallAgent/GraphOptions.cs`
- Create: `src/BusyBarOncallAgent/Graph/GraphAuthService.cs`

**Interfaces:**
- Consumes: `IGraphAuthService` from Task 3.
- Produces: `GraphAuthService : IGraphAuthService`, consumed by Task 7's DI wiring.

No automated tests in this task: the sign in flow needs a real interactive session against a real
Entra tenant, which does not exist until the Entra app registration described in this project's
design spec happens (an external, manual, one-time step outside this repo). This task's
verification is a successful build only; the actual sign in flow is verified manually once that
registration exists, per this plan's Global Constraints and the design spec's testing section.

- [ ] **Step 1: Create `src/BusyBarOncallAgent/GraphOptions.cs`**

```csharp
using System.ComponentModel.DataAnnotations;

namespace BusyBarOncallAgent;

public sealed class GraphOptions
{
    public const string SectionName = "Graph";

    [Required(AllowEmptyStrings = false)]
    public required string ClientId { get; init; }

    public string TenantId { get; init; } = "organizations";

    [Range(1, int.MaxValue)]
    public int PollIntervalSeconds { get; init; } = 30;
}
```

- [ ] **Step 2: Create `src/BusyBarOncallAgent/Graph/GraphAuthService.cs`**

```csharp
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace BusyBarOncallAgent.Graph;

/// <summary>MSAL.NET wrapper: interactive sign in on first use (Windows broker where available,
/// falling back to the system browser), silent token refresh after that, and a disk cache so the
/// user is not re-prompted on every launch. Requests Presence.Read and Calendars.Read.</summary>
public sealed class GraphAuthService : IGraphAuthService
{
    private static readonly string[] Scopes = ["Presence.Read", "Calendars.Read"];

    private readonly IPublicClientApplication _app;

    public GraphAuthService(IOptions<GraphOptions> options)
    {
        var graphOptions = options.Value;
        _app = PublicClientApplicationBuilder.Create(graphOptions.ClientId)
            .WithAuthority($"https://login.microsoftonline.com/{graphOptions.TenantId}")
            .WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows))
            .WithDefaultRedirectUri()
            .Build();

        var cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "busybar-oncall-agent");
        var storageProperties = new StorageCreationPropertiesBuilder("msal_cache.bin", cacheDir).Build();
        var cacheHelper = MsalCacheHelper.CreateAsync(storageProperties).GetAwaiter().GetResult();
        cacheHelper.RegisterCache(_app.UserTokenCache);
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var accounts = await _app.GetAccountsAsync().ConfigureAwait(false);
        var account = accounts.FirstOrDefault();

        if (account is not null)
        {
            try
            {
                var silentResult = await _app.AcquireTokenSilent(Scopes, account)
                    .ExecuteAsync(cancellationToken).ConfigureAwait(false);
                return silentResult.AccessToken;
            }
            catch (MsalUiRequiredException)
            {
                // Falls through to interactive sign in below.
            }
        }

        var interactiveResult = await _app.AcquireTokenInteractive(Scopes)
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return interactiveResult.AccessToken;
    }

    public async Task SignOutAsync()
    {
        var accounts = await _app.GetAccountsAsync().ConfigureAwait(false);
        foreach (var account in accounts)
        {
            await _app.RemoveAsync(account).ConfigureAwait(false);
        }
    }
}
```

- [ ] **Step 3: Build**

Run (PowerShell, from the repo root):

```powershell
dotnet build BusyBarOncallAgent.sln
```

Expected: build succeeds with 0 errors. `GraphOptions.ClientId` has no default and
`[Required(AllowEmptyStrings = false)]`, so this will surface as a startup validation failure
once Task 7 wires `ValidateOnStart()` in, until a real client ID is configured — that is
intentional fail-fast behavior, not a bug to fix here.

- [ ] **Step 4: Commit**

```bash
git add src/BusyBarOncallAgent/GraphOptions.cs src/BusyBarOncallAgent/Graph/GraphAuthService.cs
git commit -m "Add GraphAuthService (MSAL.NET)"
```

---

### Task 7: Tray application host, orchestration, and the full README

**Files:**
- Create: `src/BusyBarOncallAgent/BusyBarOptions.cs`
- Create: `src/BusyBarOncallAgent/MicOptions.cs`
- Create: `src/BusyBarOncallAgent/TrayApplicationContext.cs`
- Modify: `src/BusyBarOncallAgent/BusyBarOncallAgent.csproj`
- Modify: `src/BusyBarOncallAgent/Program.cs`
- Modify: `README.md`

**Interfaces:**
- Consumes: everything from Tasks 2 through 6 (`OnCallStateEvaluator`, `IGraphPresenceClient`,
  `IGraphAuthService`, `MicrophoneActivityDetector`, `IMicrophoneRegistryReader`,
  `BusyBarTimerController`, `GraphOptions`, `GraphAuthService`).
- Produces: the runnable tray application. Nothing downstream consumes this task, since it is the
  last one.

- [ ] **Step 1: Create `src/BusyBarOncallAgent/BusyBarOptions.cs`**

```csharp
using System.ComponentModel.DataAnnotations;

namespace BusyBarOncallAgent;

public sealed class BusyBarOptions
{
    public const string SectionName = "BusyBar";

    [Required]
    public string Address { get; init; } = "10.0.4.20";
}
```

- [ ] **Step 2: Create `src/BusyBarOncallAgent/MicOptions.cs`**

```csharp
using System.ComponentModel.DataAnnotations;

namespace BusyBarOncallAgent;

public sealed class MicOptions
{
    public const string SectionName = "Mic";

    public bool Enabled { get; init; } = true;

    [Range(1, int.MaxValue)]
    public int PollIntervalSeconds { get; init; } = 5;
}
```

- [ ] **Step 3: Modify `src/BusyBarOncallAgent/BusyBarOncallAgent.csproj`**

Add `<OutputType>WinExe</OutputType>` to the `<PropertyGroup>` (so running the app does not open a
console window), directly below `<UseWindowsForms>true</UseWindowsForms>`:

```xml
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <OutputType>WinExe</OutputType>
```

- [ ] **Step 4: Create `src/BusyBarOncallAgent/TrayApplicationContext.cs`**

```csharp
using BusyBarOncallAgent.Graph;
using BusyBarOncallAgent.Mic;
using BusyBarOncallAgent.OnCall;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusyBarOncallAgent;

/// <summary>The tray application's shell: a NotifyIcon with no main window, hosting the two
/// polling timers and applying the resulting OnCallState to the BUSY Bar. Both timers run on
/// this class's own thread (the WinForms UI thread), since the Win32 APIs behind the tray icon
/// and the registry reads need to run there.</summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _icon;
    private readonly System.Windows.Forms.Timer _graphTimer;
    private readonly System.Windows.Forms.Timer _micTimer;
    private readonly IGraphPresenceClient _presenceClient;
    private readonly IGraphAuthService _authService;
    private readonly MicrophoneActivityDetector _micDetector;
    private readonly BusyBarTimerController _timerController;
    private readonly ILogger<TrayApplicationContext> _logger;

    private GraphPresenceSnapshot? _latestPresence;
    private DateTimeOffset? _latestMeetingEnd;
    private bool _latestMicActive;
    private bool _pollingPaused;

    public TrayApplicationContext(
        IGraphPresenceClient presenceClient,
        IGraphAuthService authService,
        MicrophoneActivityDetector micDetector,
        BusyBarTimerController timerController,
        IOptions<GraphOptions> graphOptions,
        IOptions<MicOptions> micOptions,
        ILogger<TrayApplicationContext> logger)
    {
        _presenceClient = presenceClient;
        _authService = authService;
        _micDetector = micDetector;
        _timerController = timerController;
        _logger = logger;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Sign in", null, async (_, _) => await SignInAsync().ConfigureAwait(true));
        menu.Items.Add("Sign out", null, async (_, _) => await _authService.SignOutAsync().ConfigureAwait(true));
        menu.Items.Add("Pause", null, (_, _) => _pollingPaused = !_pollingPaused);
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "BUSY Bar on call agent",
            ContextMenuStrip = menu,
            Visible = true
        };

        _graphTimer = new System.Windows.Forms.Timer { Interval = graphOptions.Value.PollIntervalSeconds * 1000 };
        _graphTimer.Tick += async (_, _) => await PollGraphAsync().ConfigureAwait(true);
        _graphTimer.Start();

        _micTimer = new System.Windows.Forms.Timer { Interval = micOptions.Value.PollIntervalSeconds * 1000 };
        _micTimer.Tick += async (_, _) => await PollMicrophoneAsync().ConfigureAwait(true);
        if (micOptions.Value.Enabled)
        {
            _micTimer.Start();
        }
    }

    private async Task PollGraphAsync()
    {
        if (_pollingPaused)
        {
            return;
        }

        try
        {
            _latestPresence = await _presenceClient.GetPresenceAsync(CancellationToken.None).ConfigureAwait(true);
            _latestMeetingEnd = await _presenceClient.GetCurrentMeetingEndAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Graph poll failed; keeping last known presence.");
        }

        await ApplyStateAsync().ConfigureAwait(true);
    }

    private async Task PollMicrophoneAsync()
    {
        if (_pollingPaused)
        {
            return;
        }

        try
        {
            _latestMicActive = _micDetector.IsActive();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Microphone check failed; treating as inactive.");
            _latestMicActive = false;
        }

        await ApplyStateAsync().ConfigureAwait(true);
    }

    private async Task ApplyStateAsync()
    {
        var state = OnCallStateEvaluator.Evaluate(_latestPresence, _latestMeetingEnd, _latestMicActive);
        try
        {
            await _timerController.ApplyAsync(state, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply BUSY Bar timer state.");
        }
    }

    private async Task SignInAsync()
    {
        try
        {
            await _authService.GetAccessTokenAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sign in failed.");
        }
    }
}
```

- [ ] **Step 5: Replace `src/BusyBarOncallAgent/Program.cs` with**

```csharp
using Busy.Bar;
using BusyBarOncallAgent;
using BusyBarOncallAgent.Graph;
using BusyBarOncallAgent.Mic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<GraphOptions>()
    .Bind(builder.Configuration.GetSection(GraphOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<MicOptions>()
    .Bind(builder.Configuration.GetSection(MicOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<BusyBarOptions>()
    .Bind(builder.Configuration.GetSection(BusyBarOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IGraphAuthService, GraphAuthService>();
builder.Services.AddHttpClient<IGraphPresenceClient, GraphPresenceClient>(client =>
{
    client.BaseAddress = new Uri("https://graph.microsoft.com/");
});
builder.Services.AddSingleton<IMicrophoneRegistryReader, Win32MicrophoneRegistryReader>();
builder.Services.AddSingleton<MicrophoneActivityDetector>();
builder.Services.AddSingleton(provider =>
{
    var options = provider.GetRequiredService<IOptions<BusyBarOptions>>().Value;
    return new BusyBar(new BusyBarOptions_ { Addr = options.Address });
});
builder.Services.AddSingleton<BusyBarTimerController>();
builder.Services.AddSingleton<TrayApplicationContext>();

var host = builder.Build();

ApplicationConfiguration.Initialize();
Application.Run(host.Services.GetRequiredService<TrayApplicationContext>());
```

Note the `new Busy.Bar.BusyBarOptions { Addr = options.Address }` line fully qualifies the NuGet
package's own options type: this project's own `BusyBarOptions` (Step 1 of this task) and
`Busy.Bar.BusyBarOptions` share a name, and the `using Busy.Bar;` at the top of this file makes
the unqualified name `BusyBarOptions` resolve to this project's type, not the package's — the
explicit `Busy.Bar.` prefix on that one line is required, not optional style.

- [ ] **Step 6: Build**

Run (PowerShell, from the repo root):

```powershell
dotnet build BusyBarOncallAgent.sln
```

Expected: build succeeds with 0 errors.

- [ ] **Step 7: Run the full test suite**

Run (PowerShell, from the repo root):

```powershell
dotnet test BusyBarOncallAgent.sln
```

Expected: all tests from Tasks 2, 3, 4, and 5 pass (27 tests total: 9 + 5 + 6 + 7).

- [ ] **Step 8: Replace `README.md` with**

```markdown
# busybar-oncall-agent

A Windows tray app that keeps a physical BUSY Bar's built-in timer in sync with real Microsoft
Teams on call status: started (sized to your current meeting's remaining time when known) the
moment you are on a call, stopped the moment you are not.

Reads Microsoft Graph presence as its primary signal (with a calendar lookup for the timer's
length), and falls back to local microphone activity for calls Graph presence misses.

## One time setup: register an Entra app

This app needs its own Entra app registration to sign in with. Register one in
[the Entra admin center](https://entra.microsoft.com/):

1. **App registrations** then **New registration**.
2. Name it whatever you like (for example `busybar-oncall-agent`).
3. Under **Supported account types**, choose **Accounts in any organizational directory**
   (multi tenant) if you want anyone to be able to sign in without registering their own app, or
   **Accounts in this organizational directory only** if you want it restricted to your tenant.
4. Under **Redirect URI**, choose platform **Mobile and desktop applications** and add
   `http://localhost`.
5. Under **API permissions**, add Microsoft Graph delegated permissions `Presence.Read` and
   `Calendars.Read`.
6. Copy the **Application (client) ID** from the Overview page.

## Configure

Edit `src/BusyBarOncallAgent/appsettings.json` (or set the equivalent environment variables,
double underscore nesting, for example `Graph__ClientId`):

| Key | Default | Description |
| --- | --- | --- |
| `Graph:ClientId` | (none, required) | The Entra app's client ID from setup above. |
| `Graph:TenantId` | `organizations` | Which tenants can sign in; matches the app registration's account type. |
| `Graph:PollIntervalSeconds` | `30` | How often to poll presence and, when on a call, the calendar. |
| `Mic:Enabled` | `true` | Whether the local microphone fallback runs at all. |
| `Mic:PollIntervalSeconds` | `5` | How often to check local microphone activity. |
| `BusyBar:Address` | `10.0.4.20` | Network address of the BUSY Bar device. |

## Run

```bash
dotnet run --project src/BusyBarOncallAgent/BusyBarOncallAgent.csproj
```

A tray icon appears. Right click it to sign in the first time; after that, your token is cached
and refreshed silently.

## Development

```bash
dotnet build BusyBarOncallAgent.sln
dotnet test BusyBarOncallAgent.sln
```
```

- [ ] **Step 9: Commit**

```bash
git add src/BusyBarOncallAgent/BusyBarOptions.cs src/BusyBarOncallAgent/MicOptions.cs src/BusyBarOncallAgent/TrayApplicationContext.cs src/BusyBarOncallAgent/BusyBarOncallAgent.csproj src/BusyBarOncallAgent/Program.cs README.md
git commit -m "Wire up the tray application host and finish the README"
```
