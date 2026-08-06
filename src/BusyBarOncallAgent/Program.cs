using Busy.Bar;
using BusyBarOncallAgent;
using BusyBarOncallAgent.Graph;
using BusyBarOncallAgent.Mic;
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
builder.Services.AddOptions<BusyBarOncallAgent.BusyBarOptions>()
    .Bind(builder.Configuration.GetSection(BusyBarOncallAgent.BusyBarOptions.SectionName))
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
    var options = provider.GetRequiredService<IOptions<BusyBarOncallAgent.BusyBarOptions>>().Value;
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
        $"busybar-oncall-agent failed to start:\n\n{ex.Message}\n\nCheck appsettings.json (Graph:ClientId is required).",
        "busybar-oncall-agent",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);
    return;
}

ApplicationConfiguration.Initialize();
Application.Run(context);
