# oncall-busybar-agent

A Windows tray app that runs your BUSY Bar's timer while you're on a Microsoft Teams call. The
timer starts when a call starts and stops when it ends. For a scheduled meeting, it counts down to
the meeting's end time.

Teams presence from Microsoft Graph is the main signal, with a calendar lookup for the meeting's
end time. For calls Teams doesn't report, the app falls back to checking whether your microphone
is in use. Signing in is optional: without it, the microphone check works on its own.

## Documentation

Everything else (signing in, configuration, Intune and Group Policy, installing, privacy) is at
**[oncall.busy.homotechsual.dev](https://oncall.busy.homotechsual.dev)**.

## Development

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

## AI use

AI tools assist with developing this project: Claude Code for things like design and UI work,
build and release tooling, documentation and parts of the app's code, and GitHub Copilot for
autocompletion in the editor. The maintainer decides what gets built, reviews and tests every
change, and is responsible for everything that ships.
