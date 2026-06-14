using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Surfshack.Screenshots.Testing.Hosting;
using Xunit;

namespace Surfshack.Screenshots.Testing.Tests.Unit;

public class TestAuthHandlerTests
{
    [Fact]
    public async Task HandleAuthenticate_NoHeader_ReturnsNoResult()
    {
        var handler = await CreateHandlerAsync(headerValue: null);
        var result = await handler.AuthenticateAsync();
        Assert.True(result.None);
    }

    [Fact]
    public async Task HandleAuthenticate_EmptyHeader_ReturnsNoResult()
    {
        var handler = await CreateHandlerAsync(headerValue: "");
        var result = await handler.AuthenticateAsync();
        Assert.True(result.None);
    }

    [Fact]
    public async Task HandleAuthenticate_WithUserId_ReturnsSuccessWithClaims()
    {
        var handler = await CreateHandlerAsync(headerValue: "user-42");
        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        var principal = result.Principal!;
        Assert.Equal("user-42", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("screenshot-test-user", principal.FindFirstValue(ClaimTypes.Name));
        Assert.Equal("screenshot@example.test", principal.FindFirstValue(ClaimTypes.Email));
        Assert.Equal(TestAuthHandler.SchemeName, principal.Identity!.AuthenticationType);
    }

    private static async Task<TestAuthHandler> CreateHandlerAsync(string? headerValue)
    {
        var context = new DefaultHttpContext();
        if (headerValue is not null)
            context.Request.Headers[TestAuthHandler.UserHeaderName] = headerValue;

        var options = new OptionsMonitorStub(new AuthenticationSchemeOptions());
        var handler = new TestAuthHandler(options, NullLoggerFactory.Instance, UrlEncoder.Default);
        var scheme = new AuthenticationScheme(
            TestAuthHandler.SchemeName,
            displayName: null,
            handlerType: typeof(TestAuthHandler));
        await handler.InitializeAsync(scheme, context);
        return handler;
    }

    private sealed class OptionsMonitorStub(AuthenticationSchemeOptions options)
        : IOptionsMonitor<AuthenticationSchemeOptions>
    {
        public AuthenticationSchemeOptions CurrentValue => options;
        public AuthenticationSchemeOptions Get(string? name) => options;
        public IDisposable? OnChange(Action<AuthenticationSchemeOptions, string?> listener) => null;
    }
}
