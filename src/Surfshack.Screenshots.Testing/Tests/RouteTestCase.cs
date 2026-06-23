namespace Surfshack.Screenshots.Testing.Tests;

/// <summary>
/// One row in a screenshot suite's route table.
/// </summary>
/// <param name="Slug">Stable identifier used as the screenshot filename and the test display name.</param>
/// <param name="Authed">Whether the route requires the authed browser context.</param>
/// <param name="CartSessionCookie">
/// Optional cart session cookie value to inject before navigation. Use for routes that
/// need a populated server-side session (cart, checkout, etc.). The cookie name is
/// project-specific and supplied by the consumer's test class.
/// </param>
public sealed record RouteTestCase(string Slug, bool Authed, string? CartSessionCookie = null);
