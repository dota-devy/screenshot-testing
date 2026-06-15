using System.Text.Json;
using Microsoft.Playwright;
using Surfshack.Screenshots.Testing.Hosting;

namespace Surfshack.Screenshots.Testing.Fixtures;

/// <summary>
/// Constructs Playwright <see cref="IBrowserContext"/> instances configured for
/// deterministic screenshot capture: fixed viewports, animations disabled, and
/// hermetic network routing (any non-loopback request is aborted at the browser level).
/// </summary>
/// <remarks>
/// Hermetic routing is the default and keeps captures reproducible regardless of CI
/// egress. Consumers whose pages legitimately depend on external resources (web fonts,
/// icon CDNs) can pass an <c>allowedHosts</c> allowlist so those specific hosts are
/// permitted while everything else is still aborted. Allowing live hosts reintroduces a
/// network dependency, so prefer self-hosting where practical and keep the allowlist small.
/// </remarks>
public static class BrowserContextHelpers
{
    private const string NoAnimationCss = """
        *, *::before, *::after {
            animation-duration: 0s !important;
            animation-delay: 0s !important;
            transition-duration: 0s !important;
            transition-delay: 0s !important;
        }
        html { scroll-behavior: auto !important; }
    """;

    // Replaces IntersectionObserver with a shim that immediately reports every observed
    // element as intersecting, so scroll-reveal content renders in headless captures.
    private const string IntersectionObserverShim = """
        window.IntersectionObserver = class {
            constructor(callback) { this._callback = callback; }
            observe(target) {
                this._callback(
                    [{ target, isIntersecting: true, intersectionRatio: 1, time: 0 }],
                    this);
            }
            unobserve() {}
            disconnect() {}
            takeRecords() { return []; }
        };
    """;

    public static async Task<IBrowserContext> NewAnonContextAsync(
        IBrowser browser,
        ViewportSpec viewport,
        IReadOnlyCollection<string>? allowedHosts = null)
    {
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = ToPlaywrightSize(viewport),
        });
        await ConfigureContextAsync(context, allowedHosts);
        return context;
    }

    public static async Task<IBrowserContext> NewAuthedContextAsync(
        IBrowser browser,
        ViewportSpec viewport,
        string testUserId,
        IReadOnlyCollection<string>? allowedHosts = null)
    {
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = ToPlaywrightSize(viewport),
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                [TestAuthHandler.UserHeaderName] = testUserId,
            },
        });
        await ConfigureContextAsync(context, allowedHosts);
        return context;
    }

    /// <summary>
    /// Convert a <see cref="ViewportSpec"/> into Playwright's concrete <see cref="ViewportSize"/>.
    /// Kept internal-scope because consumers should work in terms of <see cref="ViewportSpec"/>
    /// and never touch Playwright's type directly.
    /// </summary>
    internal static ViewportSize ToPlaywrightSize(ViewportSpec viewport) =>
        new() { Width = viewport.Width, Height = viewport.Height };

    /// <summary>
    /// Decide whether a request should be allowed through hermetic routing. Loopback
    /// requests (the app under test) are always allowed; any other request is allowed only
    /// if its host exactly matches an entry in <paramref name="allowedHosts"/>
    /// (case-insensitive). With a null/empty allowlist this is fully hermetic.
    /// </summary>
    /// <remarks>Pulled out as an internal static so it can be unit-tested without a browser.</remarks>
    internal static bool IsRequestAllowed(string url, IReadOnlyCollection<string>? allowedHosts)
    {
        if (url.StartsWith("http://127.0.0.1", StringComparison.Ordinal)
            || url.StartsWith("http://localhost", StringComparison.Ordinal))
        {
            return true;
        }

        if (allowedHosts is null || allowedHosts.Count == 0)
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        foreach (var host in allowedHosts)
        {
            if (string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static async Task ConfigureContextAsync(
        IBrowserContext context,
        IReadOnlyCollection<string>? allowedHosts)
    {
        await context.AddInitScriptAsync(
            "const s = document.createElement('style'); " +
            $"s.textContent = {JsonSerializer.Serialize(NoAnimationCss)}; " +
            "document.head.appendChild(s);");

        // Neutralize IntersectionObserver so scroll-reveal content (a common pattern that
        // starts elements at opacity:0 and reveals them when they enter the viewport) is
        // captured fully. In a headless full-page screenshot the observer never fires for
        // off-screen elements, leaving them invisible; here every observed element is
        // reported as immediately intersecting. Installed before page scripts run.
        await context.AddInitScriptAsync(IntersectionObserverShim);

        await context.RouteAsync("**/*", async route =>
        {
            if (IsRequestAllowed(route.Request.Url, allowedHosts))
                await route.ContinueAsync();
            else
                await route.AbortAsync();
        });
    }
}
