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

    /// <summary>
    /// External hosts (exact match, case-insensitive) permitted through hermetic routing,
    /// in addition to the loopback app under test. Default is empty — fully hermetic, so
    /// every non-loopback request is aborted for reproducible captures. Override to allow
    /// resources the page genuinely needs to render faithfully, e.g.
    /// <c>["fonts.googleapis.com", "fonts.gstatic.com", "cdnjs.cloudflare.com"]</c>.
    /// </summary>
    /// <remarks>
    /// Allowing live hosts reintroduces a network dependency in CI; prefer self-hosting
    /// where practical and keep this list as small as possible.
    /// </remarks>
    protected virtual IReadOnlyCollection<string> AllowedExternalHosts => Array.Empty<string>();

    public async Task InitializeAsync()
    {
        // Construct + start the factory, retrying the known first-run TestServer race.
        Factory = await FactoryStartup.CreateStartedAsync(CreateFactory);
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
        => BrowserContextHelpers.NewAnonContextAsync(Browser, viewport, AllowedExternalHosts);

    public Task<IBrowserContext> AuthedContextAsync(ViewportSpec viewport, string testUserId)
        => BrowserContextHelpers.NewAuthedContextAsync(Browser, viewport, testUserId, AllowedExternalHosts);

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
