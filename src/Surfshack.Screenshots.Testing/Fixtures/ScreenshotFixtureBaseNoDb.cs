using Microsoft.Playwright;
using Surfshack.Screenshots.Testing.Hosting;
using Surfshack.Screenshots.Testing.Tests;
using Xunit;

namespace Surfshack.Screenshots.Testing.Fixtures;

/// <summary>
/// xUnit collection-scoped fixture base for screenshot tests against apps
/// that do not use EF Core. Sibling to <see cref="ScreenshotFixtureBase{TFactory, TDbContext}"/>;
/// drops the <c>TDbContext</c> generic, the bootstrap migration step, the connection-string
/// environment variable requirement, and the <c>PreHostBootstrapAsync</c> hook.
/// </summary>
/// <typeparam name="TFactory">Subclass of <see cref="KestrelTestFactoryBase{TProgram}"/>.</typeparam>
public abstract class ScreenshotFixtureBaseNoDb<TFactory> : IAsyncLifetime, IScreenshotFixture
    where TFactory : IKestrelTestFactory
{
    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = null!;
    public TFactory Factory { get; private set; } = default!;
    public string BaseUrl { get; private set; } = string.Empty;
    public string ScreenshotRoot { get; private set; } = string.Empty;

    /// <summary>
    /// Construct the test factory. No connection string is threaded because this
    /// base is for consumers with no database.
    /// </summary>
    protected abstract TFactory CreateFactory();

    /// <summary>
    /// Optional project-specific seeder. Default returns <c>null</c> (no seeding).
    /// Override for consumers that need to populate non-DB state before tests run
    /// (e.g., fill a distributed cache, set feature-flag values).
    /// </summary>
    protected virtual IScreenshotSeeder? Seeder => null;

    public async Task InitializeAsync()
    {
        Factory = CreateFactory();
        _ = Factory.CreateClient();  // triggers KestrelTestFactoryBase.CreateHost
        BaseUrl = Factory.ServerAddress.TrimEnd('/');

        if (Seeder is not null)
            await Seeder.SeedAsync(Factory.Services);

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });

        ScreenshotRoot = Path.Combine(AppContext.BaseDirectory, "TestResults", "screenshots");
        Directory.CreateDirectory(ScreenshotRoot);
    }

    public Task<IBrowserContext> AnonContextAsync(ViewportSpec viewport)
        => BrowserContextHelpers.NewAnonContextAsync(Browser, viewport);

    public Task<IBrowserContext> AuthedContextAsync(ViewportSpec viewport, string testUserId)
        => BrowserContextHelpers.NewAuthedContextAsync(Browser, viewport, testUserId);

    public async Task DisposeAsync()
    {
        ScreenshotIndexWriter.Write(ScreenshotRoot);

        if (Browser is not null)
            await Browser.CloseAsync();
        _playwright?.Dispose();
        if (Factory is not null)
            await Factory.DisposeAsync();
    }
}
