using Microsoft.Win32;

namespace OncallAgent.Mic;

/// <summary>Reads Windows' own record of which applications have used the microphone, from the
/// registry key that backs the OS's microphone privacy indicator.</summary>
public sealed class Win32MicrophoneRegistryReader : IMicrophoneRegistryReader
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone\NonPackaged";

    public IReadOnlyList<MicrophoneAppUsage> ReadAppUsages()
    {
        using var root = Registry.CurrentUser.OpenSubKey(KeyPath);
        if (root is null)
        {
            return [];
        }

        var usages = new List<MicrophoneAppUsage>();
        foreach (var appId in root.GetSubKeyNames())
        {
            using var appKey = root.OpenSubKey(appId);
            if (appKey is null)
            {
                continue;
            }

            usages.Add(new MicrophoneAppUsage(
                appId,
                ReadFileTime(appKey, "LastUsedTimeStart"),
                ReadFileTime(appKey, "LastUsedTimeStop")));
        }

        return usages;
    }

    private static DateTimeOffset? ReadFileTime(RegistryKey key, string valueName)
    {
        if (key.GetValue(valueName) is not long fileTime || fileTime == 0)
        {
            return null;
        }

        return DateTimeOffset.FromFileTime(fileTime);
    }
}
