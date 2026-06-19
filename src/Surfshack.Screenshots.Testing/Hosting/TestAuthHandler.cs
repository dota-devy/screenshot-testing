using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Surfshack.Screenshots.Testing.Hosting;

/// <summary>
/// Authentication handler for screenshot tests. Reads the <see cref="UserHeaderName"/> request
/// header (set by <c>BrowserContext.ExtraHTTPHeaders</c>) and constructs a <see cref="ClaimsPrincipal"/>
/// for the supplied user id. Wired up automatically by <see cref="KestrelTestFactoryBase{TProgram}"/>.
/// </summary>
public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>Name of the authentication scheme this handler registers and is wired to as the default.</summary>
    public const string SchemeName = "Test";

    /// <summary>Request header carrying the test user id (<c>X-Test-User</c>). Set per browser context.</summary>
    public const string UserHeaderName = "X-Test-User";

    /// <summary>
    /// Authenticates the request from the <see cref="UserHeaderName"/> header: returns a successful
    /// ticket with a <see cref="ClaimsPrincipal"/> for the supplied user id, or
    /// <see cref="AuthenticateResult.NoResult"/> when the header is absent or empty.
    /// </summary>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeaderName, out var userIdValues))
            return Task.FromResult(AuthenticateResult.NoResult());

        var userId = userIdValues.ToString();
        if (string.IsNullOrEmpty(userId))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, "screenshot-test-user"),
            new Claim(ClaimTypes.Email, "screenshot@example.test"),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
