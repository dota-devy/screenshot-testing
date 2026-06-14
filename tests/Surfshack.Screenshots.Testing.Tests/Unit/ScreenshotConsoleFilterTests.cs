using Surfshack.Screenshots.Testing;
using Xunit;

namespace Surfshack.Screenshots.Testing.Tests.Unit;

public class ScreenshotConsoleFilterTests
{
    [Theory]
    [InlineData("Failed to load resource: net::ERR_FAILED")]
    [InlineData("net::ERR_BLOCKED_BY_CLIENT at https://js.stripe.com/")]
    [InlineData("Access to font at 'https://cdn.jsdelivr.net/...' has been blocked by CORS policy")]
    [InlineData("Access to script at 'https://example.com/foo.js' has been blocked")]
    [InlineData("Access to stylesheet at 'https://example.com/foo.css' has been blocked")]
    [InlineData("Access to fetch at 'https://example.com/api' has been blocked")]
    public void IsIgnorableNetworkError_ReturnsTrue_ForKnownNetworkErrors(string text)
    {
        Assert.True(ScreenshotConsoleFilter.IsIgnorableNetworkError(text));
    }

    [Theory]
    [InlineData("Uncaught ReferenceError: foo is not defined")]
    [InlineData("TypeError: Cannot read property 'bar' of undefined")]
    [InlineData("SyntaxError: Unexpected token '{'")]
    [InlineData("")]
    public void IsIgnorableNetworkError_ReturnsFalse_ForRealJavaScriptErrors(string text)
    {
        Assert.False(ScreenshotConsoleFilter.IsIgnorableNetworkError(text));
    }
}
