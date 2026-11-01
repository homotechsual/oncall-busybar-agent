using Windows.ApplicationModel;

namespace OncallAgent.Packaging;

/// <summary>Packaged-app equivalent of StartupShortcutManager: toggles the windows.startupTask
/// extension declared in Package.appxmanifest (TaskId below must match that declaration exactly)
/// instead of writing a Startup-folder shortcut, since an MSIX install folder is read-only and
/// gets replaced on every update: a hand-written .lnk file there wouldn't survive an update, and
/// Store certification expects the native extension for this anyway.</summary>
public sealed class PackagedStartupTaskManager : IStartupShortcutManager
{
    public const string TaskId = "OncallBusyBarAgentStartup";

    public async Task<bool> IsEnabledAsync()
    {
        var task = await GetTaskAsync().ConfigureAwait(false);
        return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
    }

    public async Task<bool> EnableAsync()
    {
        var task = await GetTaskAsync().ConfigureAwait(false);
        var state = await task.RequestEnableAsync().AsTask().ConfigureAwait(false);
        return state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
    }

    public async Task<bool> DisableAsync()
    {
        var task = await GetTaskAsync().ConfigureAwait(false);
        task.Disable();
        // Re-fetching via GetTaskAsync() after Disable() rather than trusting the original task's
        // State to have updated in place. That's safer given WinRT's actual refresh semantics aren't
        // something verifiable without a real packaged run.
        var afterTask = await GetTaskAsync().ConfigureAwait(false);
        return afterTask.State is StartupTaskState.Disabled or StartupTaskState.DisabledByUser;
    }

    private static Task<StartupTask> GetTaskAsync() => StartupTask.GetAsync(TaskId).AsTask();
}
