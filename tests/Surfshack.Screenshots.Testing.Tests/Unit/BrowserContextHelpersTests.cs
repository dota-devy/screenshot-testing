using Surfshack.Screenshots.Testing.Fixtures;
using Xunit;

namespace Surfshack.Screenshots.Testing.Tests.Unit;

public class BrowserContextHelpersTests
{
    private static readonly string[] _allow = ["fonts.googleapis.com", "cdnjs.cloudflare.com"];

    [Theory]
    [InlineData("http://127.0.0.1:5000/")]
    [InlineData("http://127.0.0.1:8080/widgets/1")]
    [InlineData("http://localhost:5000/")]
    public void Loopback_is_always_allowed_even_with_no_allowlist(string url)
        => Assert.True(BrowserContextHelpers.IsRequestAllowed(url, null));

    [Theory]
    [InlineData("https://fonts.googleapis.com/css2?family=Inter")]
    [InlineData("https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css")]
    public void External_host_on_allowlist_is_allowed(string url)
        => Assert.True(BrowserContextHelpers.IsRequestAllowed(url, _allow));

    [Fact]
    public void External_host_match_is_case_insensitive()
        => Assert.True(BrowserContextHelpers.IsRequestAllowed("https://FONTS.GoogleAPIs.com/css2", _allow));

    [Theory]
    [InlineData("https://evil.example.com/track.js")]
    [InlineData("https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js")]
    public void External_host_not_on_allowlist_is_blocked(string url)
        => Assert.False(BrowserContextHelpers.IsRequestAllowed(url, _allow));

    [Theory]
    [InlineData("https://fonts.googleapis.com/css2?family=Inter")]
    [InlineData("https://cdnjs.cloudflare.com/x.css")]
    public void Empty_or_null_allowlist_blocks_all_external(string url)
    {
        Assert.False(BrowserContextHelpers.IsRequestAllowed(url, null));
        Assert.False(BrowserContextHelpers.IsRequestAllowed(url, System.Array.Empty<string>()));
    }

    [Fact]
    public void Allowlist_matches_host_exactly_not_by_suffix()
        // Allowing the apex must not implicitly allow a subdomain.
        => Assert.False(BrowserContextHelpers.IsRequestAllowed(
            "https://fonts.googleapis.com/css2", ["googleapis.com"]));

    [Fact]
    public void Malformed_url_is_blocked()
        => Assert.False(BrowserContextHelpers.IsRequestAllowed("not a url", _allow));
}
