using System.ComponentModel.DataAnnotations;
using OncallAgent.Config;

namespace OncallAgent;

public sealed class BusyBarOptions
{
    public const string SectionName = "BusyBar";

    [Required]
    public string Address { get; init; } = "10.0.4.20";

    /// <summary>The device's HTTP API password, when one is set. Only the Wi-Fi HTTP API uses it;
    /// the USB connection at 10.0.4.20 never asks for one.</summary>
    public string? HttpAccessPassword { get; init; }

    /// <summary>The password as the settings window saves it, encrypted with
    /// <see cref="HttpAccessPasswordProtector"/>. A plain <see cref="HttpAccessPassword"/> from any
    /// layer (an admin's policy above all) takes precedence over it.</summary>
    public string? HttpAccessPasswordProtected { get; init; }

    public string? ResolveHttpAccessPassword()
    {
        if (!string.IsNullOrEmpty(HttpAccessPassword))
        {
            return HttpAccessPassword;
        }

        return string.IsNullOrEmpty(HttpAccessPasswordProtected)
            ? null
            : HttpAccessPasswordProtector.TryUnprotect(HttpAccessPasswordProtected);
    }

    public Busy.Bar.BusyBar CreateClient() => new(new Busy.Bar.BusyBarOptions
    {
        Addr = Address,
        HttpAccessPassword = ResolveHttpAccessPassword()
    });
}
