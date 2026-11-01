namespace OncallAgent.Graph;

/// <summary>Thrown when a Graph call needs a token but nobody is signed in, or the cached sign-in
/// can't be refreshed without the user (an expired session or a new permission to consent to).
/// <see cref="HadAccount"/> tells the two apart.</summary>
public sealed class NotSignedInException : Exception
{
    public NotSignedInException(bool hadAccount, Exception? innerException = null)
        : base(hadAccount ? "The sign-in needs to be renewed." : "Nobody is signed in.", innerException)
    {
        HadAccount = hadAccount;
    }

    public bool HadAccount { get; }
}
