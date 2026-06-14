namespace Surfshack.Screenshots.Testing.Fixtures;

/// <summary>
/// Project-specific data seed contract. Implementations populate whatever
/// rows the screenshot routes need to render successfully (books, users,
/// orders, carts, etc.). Called from
/// <c>ScreenshotFixtureBase.InitializeAsync</c> after the test factory has
/// been constructed and host is started, so <c>services</c> resolves to the
/// running app's DI container.
/// </summary>
public interface IScreenshotSeeder
{
    Task SeedAsync(IServiceProvider services);
}
