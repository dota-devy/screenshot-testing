using Microsoft.Playwright;

namespace Surfshack.Screenshots.Testing.Fixtures;

/// <summary>
/// Marker interface implemented by <see cref="ScreenshotFixtureBase{TFactory, TDbContext}"/>
/// and <see cref="ScreenshotFixtureBaseNoDb{TFactory}"/>. Lets
/// <see cref="Tests.ScreenshotTestsBase{TFixture}"/> constrain its generic type
/// parameter without depending on the full base class shape.
/// </summary>
public interface IScreenshotFixture
{
    /// <summary>
    /// The shared headless Chromium instance launched once per fixture. New browser
    /// contexts (one per screenshot) are created from it via <see cref="AnonContextAsync"/>
    /// and <see cref="AuthedContextAsync"/>.
    /// </summary>
    IBrowser Browser { get; }

    /// <summary>
    /// Base URL of the running application under test (e.g. <c>http://127.0.0.1:35421</c>),
    /// already trimmed of any trailing slash so routes can be appended directly.
    /// </summary>
    string BaseUrl { get; }

    /// <summary>
    /// Absolute path to the root directory screenshots are written under. Per-viewport
    /// subdirectories (<c>desktop/</c>, <c>mobile/</c>, …) are created beneath it on demand.
    /// </summary>
    string ScreenshotRoot { get; }

    /// <summary>
    /// Create a fresh anonymous (unauthenticated) browser context sized to
    /// <paramref name="viewport"/>, with animations disabled and hermetic network routing applied.
    /// </summary>
    /// <param name="viewport">Viewport dimensions for the context.</param>
    /// <returns>A new <see cref="IBrowserContext"/>; the caller owns disposal.</returns>
    Task<IBrowserContext> AnonContextAsync(ViewportSpec viewport);

    /// <summary>
    /// Create a fresh authenticated browser context sized to <paramref name="viewport"/>.
    /// The supplied <paramref name="testUserId"/> is sent on every request via the test
    /// auth header, so the app under test sees a signed-in principal.
    /// </summary>
    /// <param name="viewport">Viewport dimensions for the context.</param>
    /// <param name="testUserId">User id surfaced to the app's authentication as the signed-in user.</param>
    /// <returns>A new <see cref="IBrowserContext"/>; the caller owns disposal.</returns>
    Task<IBrowserContext> AuthedContextAsync(ViewportSpec viewport, string testUserId);
}
