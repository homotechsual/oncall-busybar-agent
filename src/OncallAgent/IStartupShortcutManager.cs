namespace OncallAgent;

public interface IStartupShortcutManager
{
    bool IsEnabled();
    void Enable();
    void Disable();
}
