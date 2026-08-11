using OncallAgent.Config;

namespace OncallAgent.Tests.Config;

internal sealed class FakePolicyRegistryReader : IPolicyRegistryReader
{
    public List<PolicyRegistryValue> Values { get; } = new();

    public IReadOnlyList<PolicyRegistryValue> ReadValues() => Values;
}
