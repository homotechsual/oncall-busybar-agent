using Microsoft.Win32;

namespace OncallAgent.Config;

/// <summary>Reads IT-managed policy overrides from HKLM\SOFTWARE\Policies\OncallBusyBarAgent, one
/// subkey per appsettings.json section (Graph, Mic, BusyBar) and one registry value per config key
/// within it — the standard "machine policy" registry convention (matching how e.g. Chrome and
/// Edge expose IT-managed settings), so any MDM tool (Intune Settings Catalog, a plain deployment
/// script, Group Policy) can populate it without this app caring how the values got there.</summary>
public sealed class Win32PolicyRegistryReader : IPolicyRegistryReader
{
    private const string RootKeyPath = @"SOFTWARE\Policies\OncallBusyBarAgent";

    public IReadOnlyList<PolicyRegistryValue> ReadValues()
    {
        using var root = Registry.LocalMachine.OpenSubKey(RootKeyPath);
        if (root is null)
        {
            return [];
        }

        var values = new List<PolicyRegistryValue>();
        foreach (var sectionName in root.GetSubKeyNames())
        {
            using var sectionKey = root.OpenSubKey(sectionName);
            if (sectionKey is null)
            {
                continue;
            }

            foreach (var valueName in sectionKey.GetValueNames())
            {
                var value = sectionKey.GetValue(valueName);
                if (value is not null)
                {
                    values.Add(new PolicyRegistryValue(sectionName, valueName, value));
                }
            }
        }

        return values;
    }
}
