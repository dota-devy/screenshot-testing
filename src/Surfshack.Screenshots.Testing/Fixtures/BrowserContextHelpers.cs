using System.Text.Json;
using Microsoft.Playwright;
using Surfshack.Screenshots.Testing.Hosting;

namespace Surfshack.Screenshots.Testing.Fixtures;

/// <summary>
/// Constructs Playwright <see cref="IBrowserContext"/> instances configured for
/// deterministic screenshot capture: fixed viewports, animations disabled, and
/// hermetic network routing (any non-loopback request is aborted at the browser level).
/// </summary>
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

    public static async Task<IBrowserContext> NewAnonContextAsync(IBrowser browser, ViewportSpec viewport)
    {
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = ToPlaywrightSize(viewport),
        });
        await ConfigureContextAsync(context);
        return context;
    }

    public static async Task<IBrowserContext> NewAuthedContextAsync(
        IBrowser browser,
        ViewportSpec viewport,
        string testUserId)
    {
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = ToPlaywrightSize(viewport),
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                [TestAuthHandler.UserHeaderName] = testUserId,
            },
        });
        await ConfigureContextAsync(context);
        return context;
    }

    /// <summary>
    /// Convert a <see cref="ViewportSpec"/> into Playwright's concrete <see cref="ViewportSize"/>.
    /// Kept internal-scope because consumers should work in terms of <see cref="ViewportSpec"/>
    /// and never touch Playwright's type directly.
    /// </summary>
    internal static ViewportSize ToPlaywrightSize(ViewportSpec viewport) =>
        new() { Width = viewport.Width, Height = viewport.Height };

    private static async Task ConfigureContextAsync(IBrowserContext context)
    {
        await context.AddInitScriptAsync(
            "const s = document.createElement('style'); " +
            $"s.textContent = {JsonSerializer.Serialize(NoAnimationCss)}; " +
            "document.head.appendChild(s);");

        await context.RouteAsync("**/*", async route =>
        {
            var url = route.Request.Url;
            if (url.StartsWith("http://127.0.0.1", StringComparison.Ordinal)
                || url.StartsWith("http://localhost", StringComparison.Ordinal))
            {
                await route.ContinueAsync();
            }
            else
            {
                await route.AbortAsync();
            }
        });
    }
}
