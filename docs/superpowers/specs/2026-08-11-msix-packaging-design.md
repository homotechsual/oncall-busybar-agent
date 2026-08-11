# MSIX Packaging and Store Distribution Design

## Context

`oncall-busybar-agent` today ships as a self-contained single-file `.exe`, published manually and
copied wherever the user wants to run it from (see the README's "Run (production)" section). It is
unsigned, which the README already flags as a SmartScreen/EDR friction point, and every deployer is
expected to register their own Entra app and hand-edit `appsettings.json` next to the exe.

Distributing through the Microsoft Store as an MSIX removes the signing problem for free (the
Store re-signs the package with a Microsoft certificate after certification — no CA-purchased code
signing certificate needed), and Individual developer registration in Partner Center is free too.
This spec covers what changes in the codebase to make that possible, plus a related config need
that surfaced while scoping it: today's "everyone registers their own Entra app" model doesn't fit
a public Store listing, so this also introduces a default MJCO-owned Entra app plus an
Intune-manageable configuration override layer, so an organization deploying this via Intune can
lock down `BusyBar:Address`, `Graph:TenantId`, etc. without editing files by hand.

The unpackaged self-contained exe path is **not** going away — it keeps working exactly as
documented today, for anyone who doesn't want the Store (locked-down corp machines, or just
preferring a plain exe). MSIX becomes an additional, primary distribution path, not a replacement.

## Goal

1. Add an MSIX packaging project so the app can be submitted to the Microsoft Store, signed for
   free by Microsoft.
2. Make the startup-on-login mechanism and configuration loading work correctly under both
   packaged (MSIX) and unpackaged (today's self-contained exe) execution.
3. Ship a default, MJCO-owned Entra app registration so a Store install doesn't require every
   organization to register their own, while still letting IT override any setting (including
   which Entra app to use) via a machine policy an MDM tool like Intune can push.

## Architecture

```
Program.cs startup
       |
       v
IPackagedAppContext.IsPackaged  (GetCurrentPackageFullName P/Invoke)
       |                                  |
   false (unpackaged)                 true (packaged/MSIX)
       |                                  |
       v                                  v
StartupShortcutManager              PackagedStartupTaskManager
(existing, unchanged:               (new: Windows.ApplicationModel
 Startup-folder .lnk via              .StartupTask WinRT API, backed
 IShellLinkW/IPersistFile)            by a desktop:StartupTask
                                       manifest extension)
```

Both implementations satisfy the existing `IStartupShortcutManager` interface; `TrayApplicationContext`
does not change beyond picking the implementation via DI based on `IPackagedAppContext.IsPackaged`.

Configuration becomes a layered `IConfiguration` build, lowest to highest precedence:

```
bundled appsettings.json (factory defaults, incl. MJCO's default Graph:ClientId)
       -> environment variables (existing default host behavior, unchanged)
       -> %LOCALAPPDATA%\oncall-busybar-agent\appsettings.json (per-install user file, new location)
       -> HKLM\SOFTWARE\Policies\OncallBusyBarAgent\* (new: IT/Intune-managed override, always wins)
```

This layering applies identically whether the app is packaged or not — registry policy and the
LocalAppData user file both exist to serve any Windows install, MSIX or plain exe. Only the
*startup* mechanism forks on packaged vs. unpackaged.

## Components

### `IPackagedAppContext` (new)

```csharp
public interface IPackagedAppContext
{
    bool IsPackaged { get; }
}
```

Backed by a P/Invoke to `GetCurrentPackageFullName` (`kernel32.dll`): returns
`APPMODEL_ERROR_NO_PACKAGE` (15700) when the process has no package identity. Chosen over the WinRT
`Windows.ApplicationModel.Package.Current` property for this specific check because it needs no
WinRT projection and matches the plain P/Invoke style `StartupShortcutManager.cs` already uses —
the WinRT projection is still needed for the startup task API itself (see below), but not for this
cheap up-front branch.

### `PackagedStartupTaskManager` (new)

Implements `IStartupShortcutManager` using `Windows.ApplicationModel.StartupTask`:

- `IsEnabled()` → `StartupTask.GetAsync(TaskId).State is StartupTaskState.Enabled or EnabledByPolicy`.
- `Enable()` → `.RequestEnableAsync()`.
- `Disable()` → `.Disable()`.

Requires a matching `desktop:StartupTask` extension in `Package.appxmanifest`
(`Enabled="true"`, `EntryPoint="Windows.FullTrustApplication"`, a fixed `TaskId`), which lets the
task run without requiring the user to have separately consented, per the documented behavior for
packaged desktop apps. Also requires bumping `OncallAgent.csproj`'s `TargetFramework` from
`net10.0-windows` to `net10.0-windows10.0.19041.0` so the `Windows.ApplicationModel.StartupTask`
WinRT projection is available; this is additive (Windows SDK-versioned TFMs still produce a normal
.NET assembly) and does not change how the unpackaged self-contained publish works.

### `RegistryConfigurationProvider` / `RegistryConfigurationSource` (new)

A small custom `Microsoft.Extensions.Configuration` provider (no new NuGet dependency), reading
`HKLM\SOFTWARE\Policies\OncallBusyBarAgent`. Layout mirrors `appsettings.json`'s section structure —
one subkey per section, one registry value per key within it:

```
HKLM\SOFTWARE\Policies\OncallBusyBarAgent\Graph      (values: ClientId, TenantId, PollIntervalSeconds)
HKLM\SOFTWARE\Policies\OncallBusyBarAgent\Mic         (values: Enabled, PollIntervalSeconds)
HKLM\SOFTWARE\Policies\OncallBusyBarAgent\BusyBar     (values: Address)
```

Each subkey/value pair becomes a `"Section:Key"` config entry, matching
`Microsoft.Extensions.Configuration`'s standard colon-separated hierarchy so it slots into the
existing `GraphOptions`/`MicOptions`/`BusyBarOptions` binding with no changes to those classes.
Missing key or subkey contributes nothing (not an error) — IT sets only what it wants to lock down.

This is deliberately delivery-mechanism-agnostic: the app only reads the registry values, however
they got there. An Intune Settings Catalog custom OMA-URI profile, a Win32 app deployment script,
or plain Group Policy can all populate the same keys; only the app's own README needs to document
the key layout for whoever configures the MDM side.

### First-run config bootstrap

On startup, if `%LOCALAPPDATA%\oncall-busybar-agent\appsettings.json` does not exist, it is copied
from the bundled `appsettings.json` (which now ships MJCO's default `Graph:ClientId`, with
`BusyBar:Address` left as a placeholder the user or IT still has to set) before configuration is
built. All later runs load directly from the LocalAppData copy. This replaces "edit
`src/OncallAgent/appsettings.json` in place," which will not work once the app is packaged (the
MSIX install folder is read-only and gets replaced on every update) and was already a slightly
awkward pattern for the unpackaged build too (edits were lost on republish).

### Packaging project

A Windows Application Packaging Project (`OncallAgent.Package.wapproj`) added to `OncallAgent.sln`,
referencing `src/OncallAgent/OncallAgent.csproj`. Runs as a full-trust packaged (Desktop Bridge)
app, not an AppContainer-sandboxed one, so no new capability declarations are needed for the
existing Graph HTTP calls, the LAN call to the BUSY Bar, or the microphone registry reads beyond
the default `runFullTrust`. Targets x64 only for the initial submission (arm64 is a reasonable
later addition, not blocking). Package identity (`Name`, `Publisher`, `PublisherDisplayName`) gets
filled in from Partner Center's app name reservation once that exists — a manual, one-time step
outside this repo, along with the Store listing content (icons at required sizes, screenshots,
description, privacy policy copy, age rating).

## Multi-tenant auth: from bring-your-own to MJCO-default

The Entra app registration model changes from "every deployer registers their own app" to "MJCO
registers one multi-tenant app once, ships its client ID as the default." An organization
installing from the Store only needs an admin to grant consent for the existing delegated
`Presence.Read`/`Calendars.Read` permissions in their own tenant (a standard multi-tenant ISV app
consent flow), not walk through the whole app-registration and redirect-URI setup the README
documents today. Registering that app (and its `ms-appx-web://Microsoft.AAD.BrokerPlugin/<client-id>`
broker redirect URI) under MJCO's Partner Center/Entra tenant is a manual step outside this repo's
scope; this spec assumes it exists and its client ID becomes the new bundled `appsettings.json`
default.

Organizations that would rather use their own Entra app (e.g. a tenant policy blocks third-party
multi-tenant consent) still can, by setting `Graph:ClientId` in the registry policy layer or the
LocalAppData file, same as any other setting — no special-casing needed.

## Error handling

- Registry key/subkey/value missing → provider contributes nothing for that entry, falls through
  to the next layer down. Not logged as an error; this is the expected "IT didn't set this one"
  case.
- LocalAppData config file missing on first run → copied from bundled defaults; a copy failure
  (e.g. permissions) surfaces the same way other startup failures already do, via the existing
  `TrayApplicationContext` resolution try/catch in `Program.cs` and its `MessageBox.Show`.
- `PackagedStartupTaskManager`'s WinRT calls failing for any reason (shouldn't happen given the
  `IsPackaged` gate ahead of it, but WinRT surfaces are WinRT) are handled the same way the tray
  menu already handles `StartupShortcutManager` failures today — no new failure category
  introduced.

## Testing

`RegistryConfigurationProvider` gets unit tests against an injectable registry root (matching the
existing `FakeHttpMessageHandler`-style test-double pattern already used for
`GraphPresenceClientTests`), covering: missing subkey, missing individual value, and precedence
over the JSON layers. The LocalAppData first-run bootstrap gets a unit test using a temp directory
in place of `%LOCALAPPDATA%`. `IPackagedAppContext` and `PackagedStartupTaskManager` are not
unit-tested — same category as today's `StartupShortcutManager`, which also isn't (Win32/WinRT and
real package identity behavior verified against a real Windows machine, packaged and unpackaged,
at implementation time).

## What this explicitly does not include

- Registering the actual MJCO Entra app registration, the Partner Center developer account, app
  name reservation, or Store listing content (icons, screenshots, description, privacy policy
  copy, age rating) — all manual, one-time, outside-the-repo prerequisites.
- An authored ADMX/ADML template for a polished Group Policy/Intune Settings Catalog authoring
  experience. The registry provider reads plain `HKLM` values regardless of how they got set; a
  proper ADMX template (so IT gets a friendly picker UI instead of raw OMA-URI/registry pushes) is
  a reasonable later addition, not v1.
- ARM64 packaging. x64 only for the initial Store submission.
- CI/CD automation for building or submitting the MSIX (e.g. a GitHub Actions workflow driving
  `msbuild` on the `.wapproj` and Partner Center's submission API) — local/Visual Studio build and
  manual Partner Center upload for the first submission; automation is a reasonable later addition.
- Any change to `OnCallStateEvaluator`, `GraphPresenceClient`, `MicrophoneActivityDetector`, or
  `BusyBarTimerController` — none of this touches the on-call detection logic itself.
