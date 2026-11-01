using System.ComponentModel.DataAnnotations;

namespace OncallAgent;

public sealed class BusyBarOptions
{
    public const string SectionName = "BusyBar";

    [Required]
    public string Address { get; init; } = "10.0.4.20";

    /// <summary>The device's HTTP API password, when one is set. Only the Wi-Fi HTTP API uses it;
    /// the USB connection at 10.0.4.20 never asks for one.</summary>
    public string? HttpAccessPassword { get; init; }
}
