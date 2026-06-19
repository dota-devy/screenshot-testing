using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Surfshack.Screenshots.Testing.Hosting;
using Surfshack.Screenshots.Testing.Tests;
using Xunit;

namespace Surfshack.Screenshots.Testing.Fixtures;

/// <summary>
/// xUnit collection-scoped fixture base for screenshot tests. Owns the
/// <typeparamref name="TFactory"/>, the Playwright browser, the bootstrap migration
/// step, the seeder hook, and the screenshot output directory. Subclasses provide
/// the EF Core provider (Npgsql/Sqlite/etc.) and supply the project-specific seeder.
/// </summary>
/// <typeparam name="TFactory">Subclass of <see cref="KestrelTestFactoryBase{TProgram}"/>.</typeparam>
/// <typeparam name="TDbContext">The consumer's <see cref="DbContext"/>.</typeparam>
public abstract class ScreenshotFixtureBase<TFactory, TDbContext> : IAsyncLifetime, IScreenshotFixture
    where TFactory : IKestrelTestFactory
    where TDbContext : DbContext
{
    private IPlaywright? _playwright;

    /// <inheritdoc />
    public IBrowser Browser { get; private set; } = null!;

    /// <summary>
    /// The running test factory (WAF + Kestrel dual host). Exposed so tests and seeders
    /// can resolve services from <see cref="IKestrelTestFactory.Services"/> if needed.
    /// </summary>
    public TFactory Factory { get; private set; } = default!;

    /// <inheritdoc />
    public string BaseUrl { get; private set; } = string.Empty;

    /// <inheritdoc />
    public string ScreenshotRoot { get; private set; } = string.Empty;

    /// <summary>
    /// Connection string env var name. Defaults to <c>TEST_DATABASE_CONNECTION_STRING</c>,
    /// which the test runner is expected to set in CI. Override for projects using a
    /// different convention.
    /// </summary>
    protected virtual string ConnectionStringEnvVar => "TEST_DATABASE_CONNECTION_STRING";

    /// <summary>
    /// Construct a standalone <typeparamref name="TDbContext"/> from the connection string
    /// for the bootstrap migration step. Subclasses provide the EF Core provider via the
    /// returned context's options.
    /// </summary>
    protected abstract TDbContext CreateBootstrapContext(string connectionString);

    /// <summary>
    /// Construct the test factory with the connection string. Most subclasses
    /// will just <c>new TFactory(connectionString)</c>.
    /// </summary>
    protected abstract TFactory CreateFactory(string connectionString);

    /// <summary>
    /// Project-specific seeder. Resolved once per fixture instance.
    /// </summary>
    protected abstract IScreenshotSeeder Seeder { get; }

    /// <summary>
    /// Optional pre-host bootstrap hook. Runs against a standalone DbContext
    /// AFTER migrations but BEFORE the test factory is constructed. Use this
    /// to insert any data that <c>Program.cs</c>'s startup code might also try
    /// to insert (e.g., default Identity roles), so the host's check-then-insert
    /// pattern sees the row already exists and skips.
    /// </summary>
    /// <remarks>Default no-op.</remarks>
    protected virtual Task PreHostBootstrapAsync(TDbContext db) => Task.CompletedTask;

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

    /// <summary>
    /// xUnit lifecycle hook. Reads the connection string, runs migrations and the
    /// <see cref="PreHostBootstrapAsync"/> hook against a bootstrap context, starts the
    /// test factory, invokes the <see cref="Seeder"/>, then launches headless Chromium.
    /// </summary>
    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar)
            ?? throw new InvalidOperationException(
                $"{ConnectionStringEnvVar} env var must be set. " +
                "In CI this is provided by the run-tests job; locally, point it at " +
                "a Postgres (or other) instance reachable from the test process.");

        await using (var bootstrapDb = CreateBootstrapContext(connectionString))
        {
            await bootstrapDb.Database.MigrateAsync();
            await PreHostBootstrapAsync(bootstrapDb);
        }

        // Construct + start the factory, retrying the known first-run TestServer race.
        Factory = await FactoryStartup.CreateStartedAsync(() => CreateFactory(connectionString));
        BaseUrl = Factory.ServerAddress.TrimEnd('/');

        await Seeder.SeedAsync(Factory.Services);

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });

        ScreenshotRoot = Path.Combine(AppContext.BaseDirectory, "TestResults", "screenshots");
        Directory.CreateDirectory(ScreenshotRoot);
        // Per-viewport subdirectories are created on-demand by ScreenshotTestsBase.Capture
        // when first writing into that viewport. This is viewport-count-agnostic and
        // avoids needing to know the consumer's Viewports list here.
    }

    /// <inheritdoc />
    public Task<IBrowserContext> AnonContextAsync(ViewportSpec viewport)
        => BrowserContextHelpers.NewAnonContextAsync(Browser, viewport, AllowedExternalHosts);

    /// <inheritdoc />
    public Task<IBrowserContext> AuthedContextAsync(ViewportSpec viewport, string testUserId)
        => BrowserContextHelpers.NewAuthedContextAsync(Browser, viewport, testUserId, AllowedExternalHosts);

    /// <summary>
    /// xUnit lifecycle hook. Writes the screenshot <c>index.md</c>, then tears down the
    /// browser, Playwright, and the test factory.
    /// </summary>
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
