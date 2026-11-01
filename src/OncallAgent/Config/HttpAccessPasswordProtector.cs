using System.Security.Cryptography;
using System.Text;

namespace OncallAgent.Config;

/// <summary>Encrypts the BUSY Bar HTTP API password the settings window saves, with DPAPI scoped to
/// the current Windows user, so the settings file never holds it in plain text. Only the same user
/// on the same machine can decrypt it, which also means a copied settings file loses the password.</summary>
public static class HttpAccessPasswordProtector
{
    private static readonly byte[] Entropy = "oncall-busybar-agent:BusyBar:HttpAccessPassword"u8.ToArray();

    public static string Protect(string password) =>
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(password), Entropy, DataProtectionScope.CurrentUser));

    /// <summary>Returns null when the value can't be decrypted: it was written by another user or
    /// machine, or edited by hand.</summary>
    public static string? TryUnprotect(string protectedPassword)
    {
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedPassword), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }
}
