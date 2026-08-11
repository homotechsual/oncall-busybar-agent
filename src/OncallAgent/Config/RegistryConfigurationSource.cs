using Microsoft.Extensions.Configuration;

namespace OncallAgent.Config;

public sealed class RegistryConfigurationSource : IConfigurationSource
{
    /// <summary>Optional reader override for tests — a hermetic precedence test needs to supply a
    /// fake in place of the real registry. Defaults to null, which falls back to
    /// <see cref="Win32PolicyRegistryReader"/>, so existing callers (e.g.
    /// <see cref="RegistryConfigurationExtensions.AddOncallAgentPolicyOverrides"/>) need no changes.</summary>
    public IPolicyRegistryReader? Reader { get; init; }

    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new RegistryConfigurationProvider(Reader ?? new Win32PolicyRegistryReader());
}

public static class RegistryConfigurationExtensions
{
    /// <summary>Adds the HKLM\SOFTWARE\Policies\OncallBusyBarAgent registry policy layer as the
    /// highest-precedence configuration source — call this last, after every other
    /// AddJsonFile/AddEnvironmentVariables call.</summary>
    public static IConfigurationBuilder AddOncallAgentPolicyOverrides(this IConfigurationBuilder builder) =>
        builder.Add(new RegistryConfigurationSource());
}
