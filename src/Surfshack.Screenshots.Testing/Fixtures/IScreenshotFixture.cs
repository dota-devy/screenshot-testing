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
    IBrowser Browser { get; }
    string BaseUrl { get; }
    string ScreenshotRoot { get; }

    Task<IBrowserContext> AnonContextAsync(ViewportSpec viewport);
    Task<IBrowserContext> AuthedContextAsync(ViewportSpec viewport, string testUserId);
}
