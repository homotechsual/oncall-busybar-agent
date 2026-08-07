namespace OncallAgent.Graph;

/// <summary>Supplies bearer tokens for Microsoft Graph calls. Implemented by
/// <c>GraphAuthService</c> (MSAL.NET, interactive sign in with a cached, silently refreshed
/// token); this interface exists so callers do not depend on MSAL directly.</summary>
public interface IGraphAuthService
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
    Task SignOutAsync();
}
