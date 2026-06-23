using Microsoft.Playwright;
using Surfshack.Screenshots.Testing.Fixtures;
using Xunit;

namespace Surfshack.Screenshots.Testing.Tests;

/// <summary>
/// Abstract base class for screenshot test classes. Provides the parameterized
/// <see cref="Capture"/> method, hermetic console error filtering, and screenshot
/// path conventions. Subclasses supply the route table and the URL-for-slug lookup.
/// </summary>
/// <remarks>
/// Consumers must add a thin <c>[Theory] [MemberData(nameof(Cases))]</c> wrapper
/// method that delegates to <see cref="Capture"/>. xUnit's discovery machinery
/// doesn't traverse generic base-class methods for theory data sources well, so
/// the wrapper is unavoidable.
/// </remarks>
public abstract class ScreenshotTestsBase<TFixture> where TFixture : IScreenshotFixture
{
    private readonly TFixture _fixture;

    /// <summary>
    /// Initializes the base with the shared collection fixture supplying the browser,
    /// base URL, and screenshot root.
    /// </summary>
    /// <param name="fixture">The xUnit collection fixture for this screenshot suite.</param>
    protected ScreenshotTestsBase(TFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Routes captured by this test class. Order is preserved in the screenshot
    /// suite output and the index.md table of contents.
    /// </summary>
    protected abstract IEnumerable<RouteTestCase> Routes { get; }

    /// <summary>
    /// Maps a route slug to its actual URL. Called at test execution time
    /// (NOT at xUnit discovery time), so it's safe to reference dynamic values
    /// like seeded entity IDs that are populated by the fixture's
    /// <see cref="IScreenshotSeeder.SeedAsync"/>.
    /// </summary>
    protected abstract string RouteUrlFor(string slug);

    /// <summary>
    /// The user id passed to <see cref="IScreenshotFixture.AuthedContextAsync"/>
    /// for routes marked <c>Authed = true</c>. Defaults to <see cref="string.Empty"/>
    /// for consumers with no authenticated routes. Override when your
    /// <see cref="IScreenshotSeeder"/> produces a test user id.
    /// </summary>
    protected virtual string AuthedUserId => string.Empty;

    /// <summary>
    /// The cookie name used for the session-cookie injection on routes that
    /// supply a <see cref="RouteTestCase.CartSessionCookie"/>. Defaults to
    /// <c>SessionId</c>; override for projects using a different cookie name.
    /// </summary>
    protected virtual string SessionCookieName => "SessionId";

    /// <summary>
    /// Helper for subclasses to expose a <c>static</c> <c>Cases</c> member required by
    /// xUnit's <c>[MemberData]</c> attribute. Usage:
    /// <code>
    /// private static readonly ViewportSpec[] _viewports = { ViewportSpec.Desktop, ViewportSpec.Mobile };
    /// private static readonly RouteTestCase[] _routes = { new("home", false) };
    /// public static IEnumerable&lt;object[]&gt; Cases() =&gt; GetCases(_routes, _viewports);
    /// </code>
    /// </summary>
    protected static IEnumerable<object[]> GetCases(
        IEnumerable<RouteTestCase> routes,
        IEnumerable<ViewportSpec> viewports)
    {
        foreach (var viewport in viewports)
            foreach (var route in routes)
                yield return new object[]
                {
                    viewport,
                    route.Slug,
                    route.Authed,
                    route.CartSessionCookie ?? string.Empty,
                };
    }

    /// <summary>
    /// Navigates to one route in one viewport and writes a full-page PNG to
    /// <c>{ScreenshotRoot}/{viewport}/{slug}.png</c>. Asserts the response is OK (or a 302
    /// redirect), the page has a non-empty title, the file was written, and that no
    /// non-ignorable console errors occurred. This is the method consumers' <c>[Theory]</c>
    /// wrappers delegate to.
    /// </summary>
    /// <param name="viewport">Viewport to size the browser context to.</param>
    /// <param name="slug">Route identifier; resolved to a URL via <see cref="RouteUrlFor"/> and used as the filename.</param>
    /// <param name="authed">Whether to use an authenticated browser context (see <see cref="AuthedUserId"/>).</param>
    /// <param name="cartSessionCookie">
    /// Optional session-cookie value to inject before navigation (cookie name from
    /// <see cref="SessionCookieName"/>); empty to skip.
    /// </param>
    public async Task Capture(ViewportSpec viewport, string slug, bool authed, string cartSessionCookie)
    {
        await using var context = authed
            ? await _fixture.AuthedContextAsync(viewport, AuthedUserId)
            : await _fixture.AnonContextAsync(viewport);

        if (!string.IsNullOrEmpty(cartSessionCookie))
        {
            var uri = new Uri(_fixture.BaseUrl);
            await context.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Name = SessionCookieName,
                    Value = cartSessionCookie,
                    Domain = uri.Host,
                    Path = "/",
                    HttpOnly = true,
                    Secure = false,
                    SameSite = SameSiteAttribute.Lax,
                },
            });
        }

        var page = await context.NewPageAsync();

        var consoleErrors = new List<string>();
        page.Console += (_, msg) =>
        {
            if (msg.Type != "error") return;
            if (ScreenshotConsoleFilter.IsIgnorableNetworkError(msg.Text)) return;
            consoleErrors.Add(msg.Text);
        };

        var url = $"{_fixture.BaseUrl}{RouteUrlFor(slug)}";
        var response = await page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
        });
        Assert.NotNull(response);
        Assert.True(
            response!.Ok || response.Status == 302,
            $"Unexpected status {response.Status} for {url}");

        // Best-effort wait for network idle; aborted external requests should let it
        // settle quickly, but if anything stalls we still take the screenshot.
        try
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
            {
                Timeout = 5000,
            });
        }
        catch (TimeoutException)
        {
            // Tolerated — the page is rendered enough for a screenshot.
        }

        var title = await page.TitleAsync();
        Assert.False(
            string.IsNullOrWhiteSpace(title),
            $"Page title empty for {url}");

        Directory.CreateDirectory(Path.Combine(_fixture.ScreenshotRoot, viewport.Name));
        var screenshotPath = Path.Combine(_fixture.ScreenshotRoot, viewport.Name, $"{slug}.png");
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = screenshotPath,
            FullPage = true,
        });

        Assert.True(
            File.Exists(screenshotPath),
            $"Screenshot not written for {slug}/{viewport.Name}");
        Assert.Empty(consoleErrors);
    }
}
