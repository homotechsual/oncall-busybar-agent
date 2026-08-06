# busybar-oncall-agent Design

## Context

This is a new sibling project to `busybar-dotnet` (the typed .NET client for the BUSY Bar HTTP
API) and `psatool-busybar-agent` (a Halo PSA priority dashboard). Unlike `psatool-busybar-agent`,
which is a headless Linux Docker service, this project has to run on the user's own Windows
desktop: it needs to read local microphone state and hold a signed in Microsoft Graph session,
neither of which a container can do.

The goal: turn a physical BUSY Bar into an automatic "on call" indicator, driven by whether the
user is actually in a Teams call or meeting right now, with the display's countdown sized to the
meeting's real remaining length whenever that is knowable.

## Goal

A Windows system tray application that, while running, keeps the BUSY Bar's own native Busy timer
in sync with the user's real on call status: started (sized to the current meeting's remaining
time when known, otherwise open ended) the moment they are on a call, stopped the moment they are
not.

## Architecture

```
[Timer: 30s] -> GraphPresenceClient.GetPresenceAndCurrentMeetingAsync()
             -> (Activity, CurrentMeetingEndUtc?)
[Timer: 5s]  -> MicrophoneActivityDetector.IsMicrophoneActive()
             -> bool
                                         |
                                         v
             OnCallStateEvaluator.Evaluate(presence, meetingEnd, micActive)
                                         |
                                         v
                              OnCallState (pure value)
                                         |
                                         v
             BusyBarTimerController.ApplyAsync(state) -> BusyBar.Busy API
```

Two independently timed pollers feed a single pure evaluator, the same "pure function at the
core, thin I/O adapters around it" shape `psatool-busybar-agent` already uses for
`PriorityEngine`. The evaluator is the only place decision logic lives, and it is the only piece
covered by full unit tests.

### Why the device's native Busy timer, not a custom drawn screen

The BUSY Bar has a built in "Busy timer" concept (`BusySnapshotGetAsync`/`BusySnapshotSetAsync`
in `busybar-dotnet`, under `Busy.Bar`'s `Busy` namespace): start a timer with a fixed duration, an
open ended duration, or a work/rest interval, and the device renders its own countdown UI and
theme for it. That is exactly this feature's job, so this project calls that API directly instead
of hand drawing frames through the `Display` API the way `psatool-busybar-agent` does. Less code,
and it is literally what the feature exists for.

## Components

### `OnCallState` and `OnCallStateEvaluator`

The pure core. `OnCallState` is one of three cases:

```csharp
public abstract record OnCallState;
public sealed record NotOnCall : OnCallState;
public sealed record OnCallUntil(DateTimeOffset EndUtc) : OnCallState;
public sealed record OnCallIndefinite : OnCallState;
```

`OnCallStateEvaluator.Evaluate(GraphPresenceSnapshot? presence, DateTimeOffset? currentMeetingEndUtc, bool microphoneActive)`
is a pure function, no I/O:

1. If `presence.Activity` is `InACall`, `InAMeeting`, or `Presenting`: on call. If a current
   calendar event's end time is known, `OnCallUntil(that end time)`; otherwise
   `OnCallIndefinite` (an ad hoc call with no matching invite).
2. Otherwise, if the microphone is active locally: `OnCallIndefinite` (mic activity alone never
   carries a known end time). Mic is consulted only when Graph presence itself does not already
   say "on call" — it is a fallback for what presence misses, not a second independent trigger.
3. Otherwise: `NotOnCall`.

### `GraphPresenceClient`

Plain `HttpClient` calls against Microsoft Graph (no SDK dependency, matching
`HaloPsaDataProvider`'s own plain-HTTP style rather than pulling in `Microsoft.Graph`):

- `GET /v1.0/me/presence` for `availability`/`activity`.
- When activity indicates a call, `GET /v1.0/me/calendarView` for events overlapping the current
  moment, to find the soonest-ending one's `end.dateTime`.

### `MicrophoneActivityDetector`

Reads Windows' own record of microphone access:
`HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone\NonPackaged`,
where each subkey is an application that has used the microphone, with `LastUsedTimeStart`/
`LastUsedTimeStop` `REG_QWORD` (Windows `FILETIME`) values; a subkey whose stop time is `0` (or
earlier than its start time) means that application is using the microphone right now. This
mechanism is documented by the community (it backs Windows' own microphone privacy indicator) but
not by an official Microsoft API contract; exact key layout gets confirmed empirically against a
real Windows machine at implementation time, the same way `HaloPsaDataProvider`'s exact API calls
were determined against a real Halo instance rather than specified here.

### `BusyBarTimerController`

Turns an `OnCallState` into `Busy.Bar.BusyBar` calls, tracking the last state it actually applied
so it does not restart the device's timer (and its animation) every poll while nothing has
changed:

- `NotOnCall` → `BusySnapshotSetAsync` with `BusySnapshotNotStarted`, only if the device is not
  already stopped.
- `OnCallIndefinite` → start (or leave running) a timer with `BusyTimerInfiniteSettings`.
- `OnCallUntil(end)` → start a timer with `BusyTimerSimpleSettings { TotalTimeMs = remaining }`.
  Re-applied only if the target end time has moved by more than 60 seconds since the last applied
  value (calendar API rounding and clock skew should not cause needless restarts; an actual
  meeting extension should).

### `GraphAuthService`

`Microsoft.Identity.Client` (MSAL.NET) public client, `WithBroker()` for Windows Account broker
sign in where available, falling back to the system browser. Token cached to disk via
`Microsoft.Identity.Client.Extensions.Msal`'s cross platform cache (DPAPI encrypted at rest on
Windows) under `%LOCALAPPDATA%\busybar-oncall-agent\`, silently refreshed after the first
interactive sign in so the user is not re-prompted on every launch.

### Tray UI

A `NotifyIcon` with no main window (`ApplicationContext`-based WinForms app, `net10.0-windows`
with `UseWindowsForms` enabled): icon reflects current state (signed out / idle / on call),
right-click menu for sign in, sign out, pause polling, open logs, and exit. `System.Windows.Forms`
gives the tray icon and its message loop without building a full WinForms UI; the polling timers
run as `System.Windows.Forms.Timer` instances on that same UI thread, since the Win32 APIs behind
both the tray icon and the registry reads need to run there.

`Microsoft.Extensions.Hosting`'s generic host is used for configuration binding, options,
`HttpClientFactory`, and logging (matching `psatool-busybar-agent`'s conventions), but the host
does not drive the app's lifetime the way it does in a Worker Service: `Application.Run(...)`
does, since the tray icon needs a Win32 message loop, not a plain background service loop.

## Multi-tenant auth and the Entra app registration

Ships with a default Entra app registration, created as a multi-tenant app ("accounts in any
organizational directory"), requesting delegated `Presence.Read` and `Calendars.Read`. Its client
ID is a public value (not a secret, since this is a public client app) baked into
`appsettings.json` as the default `Graph:ClientId`, so anyone who clones the repo can sign in
without registering anything themselves.

`Graph:ClientId` and `Graph:TenantId` (default `organizations`, since presence and work calendars
are a work/school account feature, not a personal Microsoft account one) are both configurable,
so anyone whose own tenant policy blocks third party multi-tenant app consent (an admin can
restrict this regardless of how the app itself is configured) can register their own Entra app
and point this at it instead.

Creating that default Entra app registration is a one-time manual step in the Entra admin portal,
outside this repository's own scope, the same way this session's earlier work left Cloudflare
Pages project creation and GitHub remote setup as manual external prerequisites rather than
something a plan's tasks could automate. Until it exists, `appsettings.json` ships with
`Graph:ClientId` empty and the README documents the exact values needed to register one (redirect
URI, delegated scopes, "accounts in any organizational directory").

## Configuration

Same pattern as `psatool-busybar-agent`: `appsettings.json`, environment variables (double
underscore nesting), or `dotnet user-secrets` for local development.

| Key | Default | Description |
| --- | --- | --- |
| `Graph:ClientId` | (empty until registered) | The Entra app's (public) client ID. |
| `Graph:TenantId` | `organizations` | Which tenants can sign in; `organizations` for work/school accounts only. |
| `Graph:PollIntervalSeconds` | `30` | How often to poll presence and, when on a call, the calendar. |
| `Mic:Enabled` | `true` | Whether the local microphone fallback is consulted at all. |
| `Mic:PollIntervalSeconds` | `5` | How often to check local microphone activity. |
| `BusyBar:Address` | `10.0.4.20` | Network address of the BUSY Bar device, same convention as `psatool-busybar-agent`. |

## Error handling

- A failed Graph poll (network error, expired token, throttling) is logged and the last known
  presence is kept for this cycle rather than treated as "not on call"; retried next interval,
  matching `PollingBackgroundService`'s existing "log and retry, never crash the loop" pattern.
- A failed microphone registry read is logged and treated as "not active" for that cycle (fails
  safe toward not falsely showing busy).
- A failed BUSY Bar call is logged and retried on the next state change or poll, whichever comes
  first; the tray app keeps running either way.
- Signing out via the tray menu clears the cached token and stops Graph polling; the microphone
  fallback (if enabled) keeps working on its own until signed back in.

## Testing

`OnCallStateEvaluator` gets full unit test coverage as a pure function: every combination of
presence activity, known/unknown meeting end, and microphone state, plus the "mic is only a
fallback, not a second independent trigger" rule and the 60 second re-apply threshold in
`BusyBarTimerController`. `GraphPresenceClient` is tested the same way
`HaloPsaDataProvider`'s tests already are, against a fake `HttpMessageHandler`, not a real
tenant. `MicrophoneActivityDetector` and the tray UI itself are not unit tested (registry and
Win32 message loop behavior get verified against a real Windows machine at implementation time),
matching how `psatool-busybar-agent`'s Halo API surface was verified against a real Halo instance
rather than specified up front.

## What this explicitly does not include

- No Docker, no CI/CD workflows, no Docker Hub publishing — this is a per user desktop tool, not
  a deployed service.
- No auto start at Windows login (a reasonable later addition, not v1).
- No custom drawn `Display` screens; the device's native Busy timer covers this feature's whole
  job.
- No webhook or Graph change notification subscriptions; presence and calendar are both polled.
- No multi-account or multi-user support; one signed in account per running instance.
- No docs site (`psatool-busybar-agent`'s docs site pattern could be copied here later if wanted,
  out of scope for this spec).
