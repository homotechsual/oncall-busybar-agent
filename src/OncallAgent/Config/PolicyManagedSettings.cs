namespace OncallAgent.Config;

/// <summary>Which of the settings window's fields an admin has set through registry policy. Policy
/// outranks the user's settings file, so the window locks these rather than saving changes that
/// would never take effect.</summary>
public sealed record PolicyManagedSettings(bool Address, bool HttpAccessPassword, bool MicEnabled)
{
    public bool Any => Address || HttpAccessPassword || MicEnabled;

    public static PolicyManagedSettings From(IReadOnlyList<PolicyRegistryValue> values) => new(
        IsSet(values, BusyBarOptions.SectionName, "Address"),
        IsSet(values, BusyBarOptions.SectionName, "HttpAccessPassword"),
        IsSet(values, MicOptions.SectionName, "Enabled"));

    private static bool IsSet(IReadOnlyList<PolicyRegistryValue> values, string section, string key) =>
        values.Any(v =>
            string.Equals(v.SectionName, section, StringComparison.OrdinalIgnoreCase)
            && string.Equals(v.ValueName, key, StringComparison.OrdinalIgnoreCase));
}
