using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;

namespace BusyBarOncallAgent.Graph;

/// <summary>MSAL.NET wrapper: interactive sign in on first use (Windows broker where available,
/// falling back to the system browser), silent token refresh after that, and a disk cache so the
/// user is not re-prompted on every launch. Requests Presence.Read and Calendars.Read.</summary>
public sealed class GraphAuthService : IGraphAuthService
{
    private static readonly string[] Scopes = ["Presence.Read", "Calendars.Read"];

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
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "busybar-oncall-agent");
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

        var interactiveResult = await _app.AcquireTokenInteractive(Scopes)
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
