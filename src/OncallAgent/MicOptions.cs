using System.ComponentModel.DataAnnotations;

namespace OncallAgent;

public sealed class MicOptions
{
    public const string SectionName = "Mic";

    public bool Enabled { get; init; } = true;

    [Range(1, int.MaxValue)]
    public int PollIntervalSeconds { get; init; } = 5;
}
