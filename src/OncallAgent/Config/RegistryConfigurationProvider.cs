using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace OncallAgent.Config;

/// <summary>Turns the raw registry values IPolicyRegistryReader finds into the "Section:Key"
/// config keys Microsoft.Extensions.Configuration expects, so GraphOptions/MicOptions/
/// BusyBarOptions bind to them exactly the same as an appsettings.json section.</summary>
public sealed class RegistryConfigurationProvider(IPolicyRegistryReader reader) : ConfigurationProvider
{
    public override void Load()
    {
        Data = reader.ReadValues().ToDictionary(
            v => $"{v.SectionName}:{v.ValueName}",
            v => Convert.ToString(v.Value, CultureInfo.InvariantCulture),
            StringComparer.OrdinalIgnoreCase);
    }
}
