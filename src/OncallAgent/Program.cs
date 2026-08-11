using Busy.Bar;
using OncallAgent;
using OncallAgent.Config;
using OncallAgent.Graph;
using OncallAgent.Mic;
using OncallAgent.Packaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

// Must run before any Win32Window-backed control is created anywhere in the process — including
// the NotifyIcon/ContextMenuStrip TrayApplicationContext's constructor creates below, and the
// MessageBox.Show call in the catch block below. Moved to the very top so it also covers the
// config-bootstrap step, which now runs before the host is built.
ApplicationConfiguration.Initialize();

TrayApplicationContext context;
try
{
    // ContentRootPath defaults to Environment.CurrentDirectory, which for a GUI app depends entirely
    // on how it was launched (a shortcut's "Start in" folder, the shell's cwd when using `dotnet run
    // --project`, Explorer's cwd on double-click, etc.) — none of which reliably points at this
    // executable's own folder, where appsettings.json actually lives (copied there at build time).
    // Pinning it to AppContext.BaseDirectory makes config loading independent of launch context.
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory
    });

    // The bundled appsettings.json (loaded above via ContentRootPath) ships factory defaults and is
    // read-only once packaged as MSIX; user/IT overrides layer on top of it here, highest precedence
    // last: a per-user writable copy, then an IT-managed registry policy (see RegistryConfigurationProvider).
    var userConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "oncall-busybar-agent",
        "appsettings.json");
    UserConfigBootstrapper.EnsureUserConfigExists(
        Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
        userConfigPath);
    builder.Configuration
        .AddJsonFile(userConfigPath, optional: true, reloadOnChange: false)
        .AddOncallAgentPolicyOverrides();

    builder.Services.AddOptions<GraphOptions>()
        .Bind(builder.Configuration.GetSection(GraphOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();
    builder.Services.AddOptions<MicOptions>()
        .Bind(builder.Configuration.GetSection(MicOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();
    builder.Services.AddOptions<OncallAgent.BusyBarOptions>()
        .Bind(builder.Configuration.GetSection(OncallAgent.BusyBarOptions.SectionName))
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
        var options = provider.GetRequiredService<IOptions<OncallAgent.BusyBarOptions>>().Value;
        return new BusyBar(new Busy.Bar.BusyBarOptions { Addr = options.Address });
    });
    builder.Services.AddSingleton<BusyBarTimerController>();
    builder.Services.AddSingleton<IPackagedAppContext, PackagedAppContext>();
    builder.Services.AddSingleton<IStartupShortcutManager>(provider =>
        provider.GetRequiredService<IPackagedAppContext>().IsPackaged
            ? new PackagedStartupTaskManager()
            : new StartupShortcutManager());
    builder.Services.AddSingleton<TrayApplicationContext>();

    var host = builder.Build();
    context = host.Services.GetRequiredService<TrayApplicationContext>();
}
catch (Exception ex)
{
    MessageBox.Show(
        $"oncall-busybar-agent failed to start:\n\n{ex.Message}\n\nCheck appsettings.json (Graph:ClientId is required) and that %LOCALAPPDATA%\\oncall-busybar-agent is writable.",
        "oncall-busybar-agent",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);
    return;
}

Application.Run(context);
