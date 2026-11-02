# oncall-busybar-agent

[![Build](https://img.shields.io/github/actions/workflow/status/homotechsual/oncall-busybar-agent/build-msix.yml?branch=main&style=for-the-badge&label=Build)](https://github.com/homotechsual/oncall-busybar-agent/actions/workflows/build-msix.yml)
[![Release](https://img.shields.io/github/actions/workflow/status/homotechsual/oncall-busybar-agent/release.yml?style=for-the-badge&label=Release)](https://github.com/homotechsual/oncall-busybar-agent/actions/workflows/release.yml)
[![Docs deploy](https://img.shields.io/github/actions/workflow/status/homotechsual/oncall-busybar-agent/deploy-docs.yml?branch=main&style=for-the-badge&label=Docs%20deploy)](https://github.com/homotechsual/oncall-busybar-agent/actions/workflows/deploy-docs.yml)
[![Latest release](https://img.shields.io/github/v/release/homotechsual/oncall-busybar-agent?sort=semver&style=for-the-badge&label=Latest%20release&logo=github)](https://github.com/homotechsual/oncall-busybar-agent/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/homotechsual/oncall-busybar-agent/total?style=for-the-badge&label=Downloads)](https://github.com/homotechsual/oncall-busybar-agent/releases)
[![License: MIT](https://img.shields.io/github/license/homotechsual/oncall-busybar-agent?style=for-the-badge&label=License)](LICENSE)
[![Homotechsual Discord](https://img.shields.io/discord/1075421628424523816?style=for-the-badge&logo=discord&logoColor=white&label=Homotechsual%20Discord)](https://discord.homotechsual.dev)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/homotechsual?style=for-the-badge)](https://github.com/sponsors/homotechsual)
[![Docs](https://img.shields.io/badge/docs-oncall.busy.homotechsual.dev-a42828?style=for-the-badge)](https://oncall.busy.homotechsual.dev)

A Windows tray app that runs your BUSY Bar's timer while you're on a Microsoft Teams call. The
timer starts when a call starts and stops when it ends. For a scheduled meeting, it counts down to
the meeting's end time.

Teams presence from Microsoft Graph is the main signal, with a calendar lookup for the meeting's
end time. For calls Teams doesn't report, the app falls back to checking whether your microphone
is in use. Signing in is optional: without it, the microphone check works on its own.

<a href="https://apps.microsoft.com/detail/9P1XRPJT3126">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://oncall.busy.homotechsual.dev/img/ms-store-badge-light.svg">
    <img alt="Download from the Microsoft Store" src="https://oncall.busy.homotechsual.dev/img/ms-store-badge-dark.svg" width="161" height="44">
  </picture>
</a>

## Documentation

Everything else (signing in, configuration, Intune and Group Policy, installing, privacy) is at
**[oncall.busy.homotechsual.dev](https://oncall.busy.homotechsual.dev)**.

## Development

The app talks to the BUSY Bar through [BusyBar .NET](https://dotnet.busy.homotechsual.dev), a typed
.NET client for the BUSY Bar HTTP API ([`BusyBar` on NuGet](https://www.nuget.org/packages/BusyBar)).

```bash
dotnet build OncallAgent.sln
dotnet test OncallAgent.sln
dotnet run --project src/OncallAgent/OncallAgent.csproj
```

`dotnet run` keeps a console window open for as long as the app runs, which is handy for logs but
not for everyday use. To run without one, start the exe directly:

```bash
src\OncallAgent\bin\Debug\net10.0-windows\OncallAgent.exe
```

Building the installers is covered on the
[Development](https://oncall.busy.homotechsual.dev/development) docs page.

## Sponsoring

The app is free. If it's useful to you, you can support its development through
[GitHub Sponsors](https://github.com/sponsors/homotechsual).

## AI use

AI tools assist with developing this project: Claude Code for things like design and UI work,
build and release tooling, documentation and parts of the app's code, and GitHub Copilot for
autocompletion in the editor. The maintainer decides what gets built, reviews and tests every
change, and is responsible for everything that ships.
