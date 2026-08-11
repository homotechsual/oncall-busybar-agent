using Windows.ApplicationModel;

namespace OncallAgent.Packaging;

/// <summary>Packaged-app equivalent of StartupShortcutManager: toggles the windows.startupTask
/// extension declared in Package.appxmanifest (TaskId below must match that declaration exactly)
/// instead of writing a Startup-folder shortcut, since an MSIX install folder is read-only and
/// gets replaced on every update — a hand-written .lnk file there wouldn't survive an update, and
/// Store certification expects the native extension for this anyway.</summary>
public sealed class PackagedStartupTaskManager : IStartupShortcutManager
{
    public const string TaskId = "OncallBusyBarAgentStartup";

    public bool IsEnabled()
    {
        var task = GetTask();
        return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
    }

    public void Enable()
    {
        GetTask().RequestEnableAsync().AsTask().GetAwaiter().GetResult();
    }

    public void Disable()
    {
        GetTask().Disable();
    }

    private static StartupTask GetTask() =>
        StartupTask.GetAsync(TaskId).AsTask().GetAwaiter().GetResult();
}
