using System.ComponentModel.DataAnnotations;

namespace BusyBarOncallAgent;

public sealed class GraphOptions
{
    public const string SectionName = "Graph";

    [Required(AllowEmptyStrings = false)]
    public required string ClientId { get; init; }

    public string TenantId { get; init; } = "organizations";

    [Range(1, int.MaxValue)]
    public int PollIntervalSeconds { get; init; } = 30;
}
