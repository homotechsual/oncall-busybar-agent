namespace OncallAgent.Config;

public interface IPolicyRegistryReader
{
    IReadOnlyList<PolicyRegistryValue> ReadValues();
}

public sealed record PolicyRegistryValue(string SectionName, string ValueName, object Value);
