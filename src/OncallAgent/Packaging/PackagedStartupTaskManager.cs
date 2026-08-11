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

    public async Task<bool> IsEnabledAsync()
    {
        var task = await GetTaskAsync().ConfigureAwait(false);
        return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
    }

    public async Task EnableAsync()
    {
        var task = await GetTaskAsync().ConfigureAwait(false);
        await task.RequestEnableAsync().AsTask().ConfigureAwait(false);
    }

    public async Task DisableAsync()
    {
        var task = await GetTaskAsync().ConfigureAwait(false);
        task.Disable();
    }

    private static Task<StartupTask> GetTaskAsync() => StartupTask.GetAsync(TaskId).AsTask();
}
