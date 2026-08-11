namespace OncallAgent;

public interface IStartupShortcutManager
{
    Task<bool> IsEnabledAsync();
    Task EnableAsync();
    Task DisableAsync();
}
