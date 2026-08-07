using System.ComponentModel.DataAnnotations;

namespace OncallAgent;

public sealed class BusyBarOptions
{
    public const string SectionName = "BusyBar";

    [Required]
    public string Address { get; init; } = "10.0.4.20";
}
