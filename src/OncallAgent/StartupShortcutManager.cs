using IWshRuntimeLibrary;

namespace OncallAgent;

public interface IStartupShortcutManager
{
    bool IsEnabled();
    void Enable();
    void Disable();
}

/// <summary>Toggles a Windows Startup-folder shortcut rather than a registry Run key: registry
/// Run keys are the most heavily monitored autorun technique by security/EDR tooling, and a
/// shortcut is just as effective for a per-user tray app.</summary>
public sealed class StartupShortcutManager : IStartupShortcutManager
{
    private const string ShortcutFileName = "oncall-busybar-agent.lnk";

    private static string ShortcutPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), ShortcutFileName);

    public bool IsEnabled() => System.IO.File.Exists(ShortcutPath);

    public void Enable()
    {
        var shell = new WshShell();
        var shortcut = (IWshShortcut)shell.CreateShortcut(ShortcutPath);
        shortcut.TargetPath = Application.ExecutablePath;
        shortcut.WorkingDirectory = AppContext.BaseDirectory;
        shortcut.Description = "BUSY Bar on call agent";
        shortcut.Save();
    }

    public void Disable()
    {
        if (System.IO.File.Exists(ShortcutPath))
        {
            System.IO.File.Delete(ShortcutPath);
        }
    }
}
