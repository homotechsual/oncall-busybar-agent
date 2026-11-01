using Windows.Storage;

namespace OncallAgent.Config;

/// <summary>Where the per-user appsettings.json lives. The app always reads and writes the
/// %LOCALAPPDATA% path. Under MSIX, Windows redirects writes there into the package's private
/// LocalCache folder, so an editor launched outside the package has to be given that path instead.</summary>
public static class UserConfigLocation
{
    private const string FolderName = "oncall-busybar-agent";
    private const string FileName = "appsettings.json";

    public static string AppPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderName, FileName);

    /// <summary>The path to hand to Explorer or an editor. A file written by an MSIX install from
    /// before write virtualization was turned on stays at the real path, and the app still reads it
    /// there, so that one wins when no redirected copy exists.</summary>
    public static string GetShellPath(bool isPackaged)
    {
        if (!isPackaged)
        {
            return AppPath;
        }

        var redirected = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "Local", FolderName, FileName);
        return File.Exists(redirected) || !File.Exists(AppPath) ? redirected : AppPath;
    }
}
