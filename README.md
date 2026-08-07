# oncall-busybar-agent

A Windows tray app that keeps a physical BUSY Bar's built-in timer in sync with real Microsoft
Teams on call status: started (sized to your current meeting's remaining time when known) the
moment you are on a call, stopped the moment you are not.

Reads Microsoft Graph presence as its primary signal (with a calendar lookup for the timer's
length), and falls back to local microphone activity for calls Graph presence misses.

## One time setup: register an Entra app

This app needs its own Entra app registration to sign in with. Register one in
[the Entra admin center](https://entra.microsoft.com/):

1. **App registrations** then **New registration**.
2. Name it whatever you like (for example `oncall-busybar-agent`).
3. Under **Supported account types**, choose **Accounts in any organizational directory**
   (multi tenant) if you want anyone to be able to sign in without registering their own app, or
   **Accounts in this organizational directory only** if you want it restricted to your tenant.
4. Under **Redirect URI**, choose platform **Mobile and desktop applications** and add
   `http://localhost`.
5. Under **API permissions**, add Microsoft Graph delegated permissions `Presence.Read` and
   `Calendars.Read`.
6. Copy the **Application (client) ID** from the Overview page.

## Configure

Edit `src/OncallAgent/appsettings.json` (or set the equivalent environment variables,
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
dotnet run --project src/OncallAgent/OncallAgent.csproj
```

A tray icon appears. Right click it to sign in the first time; after that, your token is cached
and refreshed silently.

## Development

```bash
dotnet build OncallAgent.sln
dotnet test OncallAgent.sln
```
