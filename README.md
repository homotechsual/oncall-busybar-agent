# oncall-busybar-agent

A Windows tray app that keeps a physical BUSY Bar's built-in timer in sync with real Microsoft
Teams on call status: started (sized to your current meeting's remaining time when known) the
moment you are on a call, stopped the moment you are not.

Reads Microsoft Graph presence as its primary signal (with a calendar lookup for the timer's
length), and falls back to local microphone activity for calls Graph presence misses.

## Documentation

Full docs, including sign-in, configuration, Intune/Group Policy management, running in
production, and the privacy policy, live at
**[oncall.busy.homotechsual.dev](https://oncall.busy.homotechsual.dev)**.

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
