using Busy.Bar;
using OncallAgent;
using OncallAgent.Graph;
using OncallAgent.Mic;
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
builder.Services.AddSingleton<TrayApplicationContext>();

var host = builder.Build();

TrayApplicationContext context;
try
{
    context = host.Services.GetRequiredService<TrayApplicationContext>();
}
catch (Exception ex)
{
    MessageBox.Show(
        $"oncall-busybar-agent failed to start:\n\n{ex.Message}\n\nCheck appsettings.json (Graph:ClientId is required).",
        "oncall-busybar-agent",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);
    return;
}

ApplicationConfiguration.Initialize();
Application.Run(context);
