namespace OncallAgent;

public interface IStartupShortcutManager
{
    Task<bool> IsEnabledAsync();
    Task<bool> EnableAsync();
    Task<bool> DisableAsync();
}
