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
5. This app signs in via the Windows broker (WAM), which requires its own redirect URI — add a
   second entry under that same **Mobile and desktop applications** platform:
   `ms-appx-web://Microsoft.AAD.BrokerPlugin/<client-id>`, substituting the app's own client ID
   (from step 6 below) for `<client-id>`. Skipping this produces
   `AADSTS50011: The redirect URI ... does not match the redirect URIs configured for the
   application` the moment you try to sign in.
6. Under **API permissions**, add Microsoft Graph delegated permissions `Presence.Read` and
   `Calendars.Read`.
7. Copy the **Application (client) ID** from the Overview page, and go back to add it into the
   redirect URI from step 5.

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

## Run (production)

For actual day-to-day use, publish a self-contained single-file build rather than running from
`bin\Debug`: it doesn't need the .NET runtime installed, it's a single `.exe` you can drop
anywhere, and it's what you'd point a Startup shortcut at.

```bash
dotnet publish src\OncallAgent\OncallAgent.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

> If this fails with `MSB3094: "DestinationFiles" refers to 1 item(s), and "SourceFiles" refers
> to N item(s)`, the repo (or the `-o` output path) is sitting under a Windows user profile
> folder whose name contains an apostrophe (`C:\Users\O'Brien\...`) — the .NET SDK's publish
> targets mis-batch file copies when an apostrophe appears anywhere in the path. Publish from
> (or to) a path without one, e.g. `-o C:\publish\oncall-busybar-agent`.

Copy `publish\OncallAgent.exe` and `publish\appsettings.json` wherever you want to run it from
(they must stay in the same folder), edit `appsettings.json` there per the Configure section
above, then run the exe directly. A tray icon appears, with no console window. Right click it to
sign in the first time; after that, your token is cached and refreshed silently.

Right click the tray icon and check **Start with Windows** to add a shortcut to the exe in your
Startup folder, so it launches automatically at login. This uses a Startup-folder shortcut
rather than a registry Run key deliberately: Run keys are the autorun technique most heavily
scrutinized by security/EDR tooling, and a shortcut achieves the same result without tripping it.
Unchecking it removes the shortcut.

**Code signing:** the published exe is unsigned. Unsigned executables commonly trigger SmartScreen
warnings on first run and may be blocked outright by endpoint security policy in managed
environments. If that's a problem for your organization, sign `OncallAgent.exe` with your own
code-signing certificate (e.g. via `signtool sign`) as part of your build/deployment process —
that's outside the scope of this repo.

## Development

```bash
dotnet build OncallAgent.sln
dotnet test OncallAgent.sln
dotnet run --project src/OncallAgent/OncallAgent.csproj
```

Or run the debug build's exe directly (also console-free, unlike `dotnet run`):

```bash
src\OncallAgent\bin\Debug\net10.0-windows\OncallAgent.exe
```

Don't use `dotnet run` for normal/production use: it always launches through the `dotnet` CLI's
own console host, so a console window stays open and attached for as long as the app runs,
whatever the target's own output type is. `dotnet run` is fine for development iteration, where
having that console around for log output is actually useful.
