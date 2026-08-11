using Microsoft.Extensions.Configuration;

namespace OncallAgent.Config;

public sealed class RegistryConfigurationSource : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new RegistryConfigurationProvider(new Win32PolicyRegistryReader());
}

public static class RegistryConfigurationExtensions
{
    /// <summary>Adds the HKLM\SOFTWARE\Policies\OncallBusyBarAgent registry policy layer as the
    /// highest-precedence configuration source — call this last, after every other
    /// AddJsonFile/AddEnvironmentVariables call.</summary>
    public static IConfigurationBuilder AddOncallAgentPolicyOverrides(this IConfigurationBuilder builder) =>
        builder.Add(new RegistryConfigurationSource());
}
