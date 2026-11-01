using OncallAgent.Config;
using Xunit;

namespace OncallAgent.Tests.Config;

public class HttpAccessPasswordTests
{
    [Fact]
    public void Protect_RoundTrips_AndDoesNotContainThePassword()
    {
        var protectedPassword = HttpAccessPasswordProtector.Protect("s3cret");

        Assert.DoesNotContain("s3cret", protectedPassword);
        Assert.Equal("s3cret", HttpAccessPasswordProtector.TryUnprotect(protectedPassword));
    }

    [Theory]
    [InlineData("not base64!")]
    [InlineData("AAAAAAAAAAAAAAAA")]
    public void TryUnprotect_ReturnsNull_ForValuesItCannotDecrypt(string value)
    {
        Assert.Null(HttpAccessPasswordProtector.TryUnprotect(value));
    }

    [Fact]
    public void ResolveHttpAccessPassword_PrefersAPlainPassword()
    {
        var options = new BusyBarOptions
        {
            HttpAccessPassword = "from-policy",
            HttpAccessPasswordProtected = HttpAccessPasswordProtector.Protect("from-window")
        };

        Assert.Equal("from-policy", options.ResolveHttpAccessPassword());
    }

    [Fact]
    public void ResolveHttpAccessPassword_FallsBackToTheEncryptedPassword()
    {
        var options = new BusyBarOptions
        {
            HttpAccessPassword = "",
            HttpAccessPasswordProtected = HttpAccessPasswordProtector.Protect("from-window")
        };

        Assert.Equal("from-window", options.ResolveHttpAccessPassword());
    }

    [Fact]
    public void ResolveHttpAccessPassword_ReturnsNull_WhenNeitherIsSet()
    {
        Assert.Null(new BusyBarOptions { HttpAccessPassword = "" }.ResolveHttpAccessPassword());
    }
}
