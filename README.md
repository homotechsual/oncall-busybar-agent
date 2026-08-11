# oncall-busybar-agent

A Windows tray app that keeps a physical BUSY Bar's built-in timer in sync with real Microsoft
Teams on call status: started (sized to your current meeting's remaining time when known) the
moment you are on a call, stopped the moment you are not.

Reads Microsoft Graph presence as its primary signal (with a calendar lookup for the timer's
length), and falls back to local microphone activity for calls Graph presence misses.

## Sign in

No setup required to get started: this app ships with a default, shared multi-tenant Entra app
registration (the `Graph:ClientId` already in `appsettings.json`). The first time you sign in, if
your organization requires admin consent for third-party apps, ask your Entra admin to grant
consent for the delegated `Presence.Read` and `Calendars.Read` permissions this app requests —
after that, anyone in your tenant can sign in with no registration step of their own.

## Optional: register your own Entra app

Only do this if the default shared app above doesn't work for you — typically because your
tenant's Conditional Access policy blocks third-party multi-tenant app consent outright, or you'd
rather run this under an app registration you fully control. Register one in
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
8. Point this app at your new registration by setting `Graph:ClientId` (and `Graph:TenantId` if
   you restricted account types in step 3) via any of the layers in the Configure section below —
   the per-user `appsettings.json` file is the simplest for a single machine; the registry policy
   layer is the way to roll this out to a whole organization via Intune or Group Policy.

## Configure

Settings load in four layers, each overriding the one before it:

1. The bundled `src/OncallAgent/appsettings.json` — factory defaults, including the shared
   `Graph:ClientId` described above.
2. Environment variables (double underscore nesting, for example `Graph__ClientId`).
3. A per-user, writable copy at `%LOCALAPPDATA%\oncall-busybar-agent\appsettings.json`, created
   automatically from the bundled defaults the first time the app runs. Edit this file — not the
   one under `src/OncallAgent` — to change settings after that first run; it survives updates,
   which the bundled file's location does not once the app is installed as an MSIX package.
4. An IT-managed registry policy, for organizations deploying this via Intune or another MDM tool
   — see "Managing settings via Intune or Group Policy" below. This always wins over the file
   above.

| Key | Default | Description |
| --- | --- | --- |
| `Graph:ClientId` | (shared default) | The Entra app's (public) client ID. |
| `Graph:TenantId` | `organizations` | Which tenants can sign in; matches the app registration's account type. |
| `Graph:PollIntervalSeconds` | `30` | How often to poll presence and, when on a call, the calendar. |
| `Mic:Enabled` | `true` | Whether the local microphone fallback runs at all. |
| `Mic:PollIntervalSeconds` | `5` | How often to check local microphone activity. |
| `BusyBar:Address` | `10.0.4.20` | Network address of the BUSY Bar device. |

## Managing settings via Intune or Group Policy

Any of the settings above can be locked down centrally by setting registry values under
`HKLM\SOFTWARE\Policies\OncallBusyBarAgent`, one subkey per section:

```
HKLM\SOFTWARE\Policies\OncallBusyBarAgent\Graph      (values: ClientId, TenantId, PollIntervalSeconds)
HKLM\SOFTWARE\Policies\OncallBusyBarAgent\Mic         (values: Enabled, PollIntervalSeconds)
HKLM\SOFTWARE\Policies\OncallBusyBarAgent\BusyBar     (values: Address)
```

Set only the values you want to lock down — anything unset falls through to the user's own
`appsettings.json`. `Enabled` should be set as a String value (`true` or `false`); the interval and
address values can be a String or a DWORD, whichever your deployment tool makes easier. How the
registry values get set is up to you: an Intune Settings Catalog custom OMA-URI profile, a Win32
app deployment script, or plain Group Policy all work identically from this app's point of view —
it only reads the registry, it doesn't care how the values arrived.

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

If this app is installed from the Microsoft Store as an MSIX package instead, this doesn't apply —
the Store signs the package automatically as part of certification, at no cost.

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
