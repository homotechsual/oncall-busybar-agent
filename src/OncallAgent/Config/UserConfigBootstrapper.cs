namespace OncallAgent.Config;

/// <summary>Ensures a per-user, writable copy of appsettings.json exists before configuration is
/// built. The bundled appsettings.json next to the exe becomes read-only once packaged as MSIX
/// (and is overwritten on every republish even when unpackaged), so user/IT edits (BusyBar:Address
/// above all) need a location outside both of those constraints.</summary>
public static class UserConfigBootstrapper
{
    public static void EnsureUserConfigExists(string bundledConfigPath, string userConfigPath)
    {
        if (File.Exists(userConfigPath) || !File.Exists(bundledConfigPath))
        {
            return;
        }

        var userConfigDirectory = Path.GetDirectoryName(userConfigPath);
        if (!string.IsNullOrEmpty(userConfigDirectory))
        {
            Directory.CreateDirectory(userConfigDirectory);
        }

        File.Copy(bundledConfigPath, userConfigPath);
    }
}
