namespace OncallAgent.Graph;

/// <summary>Supplies bearer tokens for Microsoft Graph calls. Implemented by
/// <c>GraphAuthService</c> (MSAL.NET, interactive sign in with a cached, silently refreshed
/// token); this interface exists so callers do not depend on MSAL directly.</summary>
public interface IGraphAuthService
{
    /// <summary>Returns a token without ever showing UI. Throws <see cref="NotSignedInException"/>
    /// when nobody is signed in or the sign-in has to be renewed interactively.</summary>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);

    /// <summary>Signs in, showing the sign-in window only if a silent sign-in isn't possible.</summary>
    Task SignInAsync(CancellationToken cancellationToken);

    Task SignOutAsync();
}
