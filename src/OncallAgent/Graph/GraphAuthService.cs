using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;

namespace OncallAgent.Graph;

/// <summary>MSAL.NET wrapper: interactive sign in on first use (Windows broker where available,
/// falling back to the system browser), silent token refresh after that, and a disk cache so the
/// user is not re-prompted on every launch. Requests Presence.Read and Calendars.ReadBasic.</summary>
public sealed class GraphAuthService : IGraphAuthService
{
    private static readonly string[] Scopes = ["Presence.Read", "Calendars.ReadBasic"];

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private readonly IPublicClientApplication _app;

    public GraphAuthService(IOptions<GraphOptions> options)
    {
        var graphOptions = options.Value;
        _app = PublicClientApplicationBuilder.Create(graphOptions.ClientId)
            .WithAuthority($"https://login.microsoftonline.com/{graphOptions.TenantId}")
            .WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows))
            .WithDefaultRedirectUri()
            .Build();

        var cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "oncall-busybar-agent");
        var storageProperties = new StorageCreationPropertiesBuilder("msal_cache.bin", cacheDir).Build();
        var cacheHelper = MsalCacheHelper.CreateAsync(storageProperties).GetAwaiter().GetResult();
        cacheHelper.RegisterCache(_app.UserTokenCache);
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var accounts = await _app.GetAccountsAsync().ConfigureAwait(false);
        var account = accounts.FirstOrDefault();

        if (account is not null)
        {
            try
            {
                var silentResult = await _app.AcquireTokenSilent(Scopes, account)
                    .ExecuteAsync(cancellationToken).ConfigureAwait(false);
                return silentResult.AccessToken;
            }
            catch (MsalUiRequiredException)
            {
                // Falls through to interactive sign in below.
            }
        }

        // The WAM broker requires a parent window handle to attach its sign-in dialog to;
        // AcquireTokenInteractive throws MsalClientException("window_handle_required") without
        // one. This is a tray-only app with no main window, so the foreground window at the
        // moment sign-in is triggered (whatever the user just clicked) stands in as the parent.
        var interactiveResult = await _app.AcquireTokenInteractive(Scopes)
            .WithParentActivityOrWindow(GetForegroundWindow())
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return interactiveResult.AccessToken;
    }

    public async Task SignOutAsync()
    {
        var accounts = await _app.GetAccountsAsync().ConfigureAwait(false);
        foreach (var account in accounts)
        {
            await _app.RemoveAsync(account).ConfigureAwait(false);
        }
    }
}
