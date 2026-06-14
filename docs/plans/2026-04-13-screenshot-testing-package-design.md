# Surfshack.Screenshots.Testing — Package Design

**Date:** 2026-04-13
**Status:** Draft (pre-implementation)
**Author:** Devlin Vining

## Goal

Extract a Playwright-driven screenshot test pattern, proven on a production ASP.NET Core
application, into a reusable NuGet package so any .NET app can wire up a deterministic,
CI-only UI screenshot suite in ~50-100 lines of project-specific code.

The package captures every painful architectural decision learned during that first
implementation (dual-host WAF, hermetic network routing, role-seed pre-creation, console error filtering, etc.) so the next consumer doesn't relearn them.

## Reference Implementation

The pattern was first proven inside a production storefront application, where its commit
history was effectively a design rationale doc — every fix corresponded to a real CI failure
that future consumers would otherwise hit. The lessons worth carrying into the package:

| Lesson | What it captures |
|---|---|
| Dual-host bootstrap | Initial dual-host `WebApplicationFactory` + Kestrel pattern |
| Disposal/auth hardening | `override` not `new` on `DisposeAsync`, `PostConfigure` for auth scheme override, try/catch around Kestrel startup |
| Deterministic seeding | A stable, repeatable seeder shape |
| Fixture conventions | Collection-scoped fixture, browser context helpers, screenshot path conventions |
| Migration ordering | Bootstrap `MigrateAsync` BEFORE constructing the factory |
| Hermetic rendering | Hermetic network routing (abort non-loopback), filtered console error assertions, `NetworkIdle` timeout tolerance |
| Role pre-creation | Pre-create app-level roles in bootstrap to skip the `Program.cs` role-seed race |
| Slug vs ID | Route URLs are project-specific lookups, not raw entity IDs |

## Non-Goals

- **Visual regression diffing** (pixel comparisons / baseline storage). Out of scope; consumers can layer their own diffing on top of the captured PNGs.
- **Cross-browser support.** Chromium only. Firefox/WebKit are a future option but every additional browser doubles CI time.
- **Generic CRUD seed framework.** Each project's `IScreenshotSeeder` is hand-written against the project's domain model.
- **Local developer workflow.** This package is for CI-only use. Local dev workflows for screenshot tests are not supported by the package (consumers can wire them up themselves if desired).
- **Replacing existing `Microsoft.AspNetCore.Mvc.Testing`.** This package wraps WAF, doesn't replace it.

## Architecture

Three layers of abstraction, each generic over the consumer's `Program` and `DbContext` types:

1. **`KestrelTestFactoryBase<TProgram>`** — `WebApplicationFactory<TProgram>` subclass implementing the dual-host pattern that gives Playwright a real Kestrel-bound URL while keeping WAF's TestServer machinery happy. Consumers subclass and provide project-specific config keys (`UseSetting` calls).
2. **`ScreenshotFixtureBase<TFactory, TDbContext>`** — `IAsyncLifetime` collection fixture that owns the factory, the Playwright browser, the bootstrap migration step, the seeder hook, and the screenshot output directory. Consumers subclass and supply the seeder + any pre-host hooks (e.g., role pre-creation).
3. **`ScreenshotTestsBase<TFixture>`** — abstract test base with the parameterized `Capture` `[Theory]` method, hermetic network routing, console error filtering, screenshot path conventions, and `index.md` writer. Consumers subclass and supply the route table.

```
┌─────────────────────────────────────────────────────────────┐
│ Consumer Project (e.g., MyAppScreenshots)                   │
│                                                             │
│  MyAppTestFactory : KestrelTestFactoryBase<Program>         │
│  MyAppScreenshotFixture : ScreenshotFixtureBase<...>        │
│  MyAppScreenshotTests : ScreenshotTestsBase<...>            │
│  MyAppSeeder : IScreenshotSeeder                            │
│  Routes table (~10-30 lines)                                │
└──────────────────────────┬──────────────────────────────────┘
                           │ inherits
┌──────────────────────────▼──────────────────────────────────┐
│ Surfshack.Screenshots.Testing (this package)                │
│                                                             │
│  Hosting/                                                   │
│    KestrelTestFactoryBase<TProgram>                         │
│    TestAuthHandler                                          │
│  Fixtures/                                                  │
│    ScreenshotFixtureBase<TFactory, TDbContext>              │
│    BrowserContextHelpers                                    │
│    IScreenshotSeeder                                        │
│  Tests/                                                     │
│    ScreenshotTestsBase<TFixture>                            │
│    RouteCase                                                │
│    ScreenshotConsoleFilter                                  │
└──────────────────────────┬──────────────────────────────────┘
                           │ depends on
┌──────────────────────────▼──────────────────────────────────┐
│ Microsoft.Playwright, Microsoft.AspNetCore.Mvc.Testing,     │
│ Microsoft.EntityFrameworkCore, xunit                         │
└─────────────────────────────────────────────────────────────┘
```

### Why three layers, not one

A single base class would force consumers into a rigid template. Splitting along factory / fixture / tests boundaries means consumers can override exactly the part they need (e.g., a project with no DbContext can use a different fixture base; a project with non-standard auth can swap `TestAuthHandler`).

## Package Layout

```
screenshot-testing/
├── src/
│   └── Surfshack.Screenshots.Testing/
│       ├── Surfshack.Screenshots.Testing.csproj
│       ├── Hosting/
│       │   ├── KestrelTestFactoryBase.cs
│       │   └── TestAuthHandler.cs
│       ├── Fixtures/
│       │   ├── ScreenshotFixtureBase.cs
│       │   ├── BrowserContextHelpers.cs
│       │   └── IScreenshotSeeder.cs
│       ├── Tests/
│       │   ├── ScreenshotTestsBase.cs
│       │   ├── RouteCase.cs
│       │   └── ScreenshotConsoleFilter.cs
│       └── README.md
├── tests/
│   └── Surfshack.Screenshots.Testing.Tests/
│       └── (smoke tests against a tiny fake app — see Testing section)
├── samples/
│   └── MinimalConsumer/
│       └── (minimal end-to-end example, also used for tests/)
├── docs/
│   └── plans/
│       └── 2026-04-13-screenshot-testing-package-design.md   ← this file
├── .gitlab-ci.yml
├── README.md
└── nuget.config
```

## Components

### `KestrelTestFactoryBase<TProgram>`

Abstract base implementing the dual-host pattern.

```csharp
public abstract class KestrelTestFactoryBase<TProgram> : WebApplicationFactory<TProgram>
    where TProgram : class
{
    private IHost? _kestrelHost;
    public string ServerAddress { get; private set; } = string.Empty;

    /// <summary>
    /// Subclass hook for project-specific config (UseSetting calls, ConfigureTestServices, etc).
    /// Called inside the package's ConfigureWebHost override.
    /// </summary>
    protected abstract void ConfigureProject(IWebHostBuilder builder);

    protected sealed override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ConfigureProject(builder);
        InstallTestAuthHandler(builder);
    }

    protected sealed override IHost CreateHost(IHostBuilder builder)
    {
        var testHost = builder.Build();
        try
        {
            builder.ConfigureWebHost(b => b.UseKestrel().UseUrls("http://127.0.0.1:0"));
            _kestrelHost = builder.Build();
            _kestrelHost.Start();

            var server = _kestrelHost.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>()
                ?? throw new InvalidOperationException("Kestrel did not expose IServerAddressesFeature.");
            ServerAddress = addresses.Addresses.FirstOrDefault()
                ?? throw new InvalidOperationException("Kestrel reported no bound addresses.");

            testHost.Start();
            return testHost;
        }
        catch
        {
            _kestrelHost?.Dispose();
            _kestrelHost = null;
            testHost.Dispose();
            throw;
        }
    }

    public override async ValueTask DisposeAsync()
    {
        if (_kestrelHost is not null)
        {
            await _kestrelHost.StopAsync();
            _kestrelHost.Dispose();
        }
        await base.DisposeAsync();
    }

    private static void InstallTestAuthHandler(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services
                .AddAuthentication(opt =>
                {
                    opt.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    opt.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    opt.DefaultScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { });

            services.PostConfigure<AuthenticationOptions>(opt =>
            {
                opt.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                opt.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                opt.DefaultSignInScheme = TestAuthHandler.SchemeName;
                opt.DefaultScheme = TestAuthHandler.SchemeName;
            });
        });
    }
}
```

**Key invariants codified by this base:**
- The dual-host pattern is `sealed` — consumers cannot override `CreateHost`, ensuring everyone gets the same correct dual-host behavior. They override `ConfigureProject` only.
- `TestAuthHandler` is registered automatically. Consumers don't have to remember.
- `PostConfigure` fires unconditionally even if the consumer's app uses ASP.NET Identity.
- Disposal correctness (override not new, ordered cleanup, leak-safe try/catch) is in the base class — consumers can't get it wrong.

### `TestAuthHandler`

Verbatim from the original proof. Reads `X-Test-User` header, returns a `ClaimsPrincipal` with that user ID. Consumers don't subclass — they just use `TestAuthHandler.UserHeaderName` as a constant when configuring their browser context.

### `ScreenshotFixtureBase<TFactory, TDbContext>`

```csharp
public abstract class ScreenshotFixtureBase<TFactory, TDbContext> : IAsyncLifetime
    where TFactory : KestrelTestFactoryBase<Program>, new()  // see "Factory construction" below
    where TDbContext : DbContext
{
    private IPlaywright? _playwright;
    public IBrowser Browser { get; private set; } = null!;
    public TFactory Factory { get; private set; } = null!;
    public string BaseUrl { get; private set; } = string.Empty;
    public string ScreenshotRoot { get; private set; } = string.Empty;

    /// <summary>
    /// Connection string env var name. Defaults to TEST_DATABASE_CONNECTION_STRING
    /// to match the GitLab CI run-tests pattern; subclasses can override for projects
    /// using a different convention.
    /// </summary>
    protected virtual string ConnectionStringEnvVar => "TEST_DATABASE_CONNECTION_STRING";

    /// <summary>
    /// Construct a standalone DbContext from the connection string for the bootstrap
    /// migration step. Subclasses provide the EF Core provider (Npgsql, Sqlite, etc).
    /// </summary>
    protected abstract TDbContext CreateBootstrapContext(string connectionString);

    /// <summary>
    /// Construct the test factory with the connection string. Most subclasses
    /// will just `new TFactory(connectionString)`.
    /// </summary>
    protected abstract TFactory CreateFactory(string connectionString);

    /// <summary>
    /// Project-specific seeder. Called after migrations + pre-host hook,
    /// against the running factory's services.
    /// </summary>
    protected abstract IScreenshotSeeder Seeder { get; }

    /// <summary>
    /// Optional hook for project-specific bootstrap state that must exist
    /// BEFORE the host starts (e.g., pre-creating Identity roles to skip
    /// Program.cs role-seed races). Default no-op.
    /// </summary>
    protected virtual Task PreHostBootstrapAsync(TDbContext db) => Task.CompletedTask;

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar)
            ?? throw new InvalidOperationException(
                $"{ConnectionStringEnvVar} env var must be set.");

        await using (var bootstrapDb = CreateBootstrapContext(connectionString))
        {
            await bootstrapDb.Database.MigrateAsync();
            await PreHostBootstrapAsync(bootstrapDb);
        }

        Factory = CreateFactory(connectionString);
        _ = Factory.CreateClient();  // triggers CreateHost
        BaseUrl = Factory.ServerAddress.TrimEnd('/');

        await Seeder.SeedAsync(Factory.Services);

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });

        ScreenshotRoot = Path.Combine(AppContext.BaseDirectory, "TestResults", "screenshots");
        Directory.CreateDirectory(Path.Combine(ScreenshotRoot, "desktop"));
        Directory.CreateDirectory(Path.Combine(ScreenshotRoot, "mobile"));
    }

    public Task<IBrowserContext> AnonContextAsync(string viewport)
        => BrowserContextHelpers.NewAnonContextAsync(Browser, viewport);

    public Task<IBrowserContext> AuthedContextAsync(string viewport, string testUserId)
        => BrowserContextHelpers.NewAuthedContextAsync(Browser, viewport, testUserId);

    public async Task DisposeAsync()
    {
        ScreenshotIndexWriter.Write(ScreenshotRoot);
        if (Browser is not null) await Browser.CloseAsync();
        _playwright?.Dispose();
        if (Factory is not null) await Factory.DisposeAsync();
    }
}
```

**Key design choices:**
- **Bootstrap-then-host ordering** is enforced by the base class. Consumers can't accidentally start the host before migrations run.
- **`PreHostBootstrapAsync` is virtual, default no-op.** Consumers with role-based Identity override it to pre-create roles (e.g., a `Customer` role); consumers without Identity (e.g., a static-content site) leave it empty.
- **`Seeder` is a property, not a parameter to `InitializeAsync`.** Subclasses lazy-construct via DI or simple `new`.
- **`AuthedContextAsync` takes the user ID explicitly** rather than baking it in. Lets multi-user test scenarios work.

### `BrowserContextHelpers`

Static class with the two canonical context constructors. Encapsulates:
- Viewport size mapping (`desktop` → 1440×900, `mobile` → 390×844)
- No-animation CSS injection
- Hermetic network routing (abort all non-loopback requests)
- `X-Test-User` header injection for authed contexts

```csharp
public static class BrowserContextHelpers
{
    public static async Task<IBrowserContext> NewAnonContextAsync(IBrowser browser, string viewport)
    {
        var context = await browser.NewContextAsync(new() { ViewportSize = ViewportFor(viewport) });
        await ConfigureContextAsync(context);
        return context;
    }

    public static async Task<IBrowserContext> NewAuthedContextAsync(
        IBrowser browser, string viewport, string testUserId)
    {
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = ViewportFor(viewport),
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                [TestAuthHandler.UserHeaderName] = testUserId,
            },
        });
        await ConfigureContextAsync(context);
        return context;
    }

    private static async Task ConfigureContextAsync(IBrowserContext context)
    {
        await context.AddInitScriptAsync(/* no-animation CSS injection */);
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
```

### `IScreenshotSeeder`

Tiny interface — one method.

```csharp
public interface IScreenshotSeeder
{
    Task SeedAsync(IServiceProvider services);
}
```

Each consumer writes their own implementation against their domain model. The package provides no base class because seeding is too project-specific to abstract usefully. (We could add a `ScreenshotSeederBase<TDbContext>` helper later if patterns emerge across consumers, but YAGNI for now.)

### `ScreenshotTestsBase<TFixture>`

```csharp
public abstract class ScreenshotTestsBase<TFixture>(TFixture fixture)
    where TFixture : IScreenshotFixture  // marker interface implemented by ScreenshotFixtureBase<...>
{
    protected abstract IEnumerable<RouteCase> Routes { get; }
    protected abstract string RouteUrlFor(string slug);

    /// <summary>
    /// Hook for the test's [Theory] data generator.
    /// Consumer marks an override with [MemberData(nameof(Cases))].
    /// </summary>
    public IEnumerable<object[]> Cases()
    {
        foreach (var viewport in new[] { "desktop", "mobile" })
            foreach (var route in Routes)
                yield return new object[] { viewport, route.Slug, route.Authed, route.CartSessionCookie ?? string.Empty };
    }

    public async Task Capture(string viewport, string slug, bool authed, string cartSessionCookie)
    {
        // The canonical Capture method lifted from the original proof. Includes:
        //   - hermetic context selection (anon/authed)
        //   - cart session cookie injection (when present)
        //   - filtered console error capture (ignores network/CORS errors)
        //   - GotoAsync with WaitUntil = DOMContentLoaded
        //   - try/catch around NetworkIdle wait with 5s timeout
        //   - title non-empty assertion
        //   - full-page screenshot to TestResults/screenshots/{viewport}/{slug}.png
        //   - file existence + console error empty assertions
    }
}
```

The base class provides `Cases()` and `Capture()`. Consumer subclasses must:
1. Provide the routes via `Routes` property
2. Provide the URL lookup via `RouteUrlFor(slug)`
3. Wire xUnit by adding `[Theory] [MemberData(nameof(Cases))]` on a method that delegates to `Capture`

The `[Theory]` attribute can't be on the base class method because xUnit needs the discovery to happen at the consumer's assembly level. So consumers write a thin wrapper:

```csharp
[Collection(ScreenshotCollection.Name)]
public class MyAppScreenshotTests(MyAppScreenshotFixture fixture)
    : ScreenshotTestsBase<MyAppScreenshotFixture>(fixture)
{
    protected override IEnumerable<RouteCase> Routes => new[]
    {
        new RouteCase("home", Authed: false),
        new RouteCase("about", Authed: false),
    };

    protected override string RouteUrlFor(string slug) => slug switch
    {
        "home"  => "/",
        "about" => "/about",
        _ => throw new ArgumentOutOfRangeException(nameof(slug)),
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public Task CaptureRoute(string viewport, string slug, bool authed, string cartSessionCookie)
        => Capture(viewport, slug, authed, cartSessionCookie);
}
```

The 4 lines of xUnit boilerplate per consumer is annoying but necessary — xUnit's discovery machinery doesn't traverse generic base classes for `[Theory]` data sources well. The overhead is small and the design avoids reflection magic.

### `RouteCase`

Plain record.

```csharp
public sealed record RouteCase(string Slug, bool Authed, string? CartSessionCookie = null);
```

### `ScreenshotConsoleFilter`

Static helper containing the canonical "ignore network/CORS errors" filter logic, so it's testable in isolation.

```csharp
public static class ScreenshotConsoleFilter
{
    public static bool IsIgnorableNetworkError(string consoleText) =>
        consoleText.Contains("net::ERR_")
        || consoleText.Contains("Failed to load resource")
        || consoleText.Contains("Access to font")
        || consoleText.Contains("Access to script")
        || consoleText.Contains("Access to stylesheet")
        || consoleText.Contains("Access to fetch");
}
```

### `ScreenshotIndexWriter`

Static helper that writes the `index.md` table of contents from the screenshot output directory. Pulled out of the fixture so it can be unit-tested directly.

## Per-Consumer Surface

A new consumer needs:

| File | Approx LOC | Project-specific? |
|---|---|---|
| `XxxTestFactory.cs` | 10-15 | Yes — `UseSetting` calls for project config |
| `XxxScreenshotFixture.cs` | 20-30 | Yes — DbContext provider, `PreHostBootstrapAsync`, seeder property |
| `XxxScreenshotTests.cs` | 20-40 | Yes — route table + URL lookup |
| `XxxSeeder.cs` | 30-80 | Yes — domain-model-specific seed |
| `ScreenshotCollection.cs` | 5 | Yes (one-line `[CollectionDefinition]`) |
| `.gitlab-ci.yml` `run-tests` job | 5 | No — same CI snippet for every consumer (see [CI](#ci) / the shared template) |

Total: **~90-170 lines per project**, vs ~500 LOC if reimplementing from scratch.

## Companion Artifacts

Two adjacent deliverables that aren't part of the NuGet package itself but ship alongside it:

### 1. A Playwright-capable CI image

Screenshot tests need a Chromium browser plus its system dependencies available in CI.
The simplest path is Microsoft's official image,
[`mcr.microsoft.com/playwright/dotnet`](https://mcr.microsoft.com/product/playwright/dotnet/about),
which ships the .NET SDK plus Chromium and its system dependencies pre-installed — no per-pipeline
`apt-get` or `playwright install` step needed. Keep the image tag's Playwright version in sync
with the `Microsoft.Playwright` package reference.

Teams that maintain their own base images can instead pre-bake the same dependencies into a
custom image. The essential steps are the Chromium system libraries plus a one-time
`playwright install chromium --with-deps`, which together save the per-pipeline install time
(~200 seconds on a cold runner).

### 2. A shared CI template

When several projects adopt the package, the per-project `.gitlab-ci.yml` boilerplate (database
service, artifact paths, JUnit reporter) is worth factoring into a shared, `include`-able CI
template so each consumer only supplies its own variables:

```yaml
run-tests:
  extends: .screenshot-tests
  variables:
    POSTGRES_DB: myapp_test
    TEST_DATABASE_CONNECTION_STRING: "Host=postgres;..."
```

The template defines the `services: [postgres:16]`, the artifact paths, the JUnit reporter, and any other shared boilerplate.

## Versioning

- Package version follows SemVer: `1.0.0` for the initial release once the public API stabilizes.
- Pin tight to `Microsoft.Playwright` and `Microsoft.AspNetCore.Mvc.Testing` major versions in the `.csproj` — the dual-host pattern relies on internal WAF behavior that has shifted across .NET releases, so cross-version testing is required before any major bump.
- Consumers reference the package from whichever NuGet feed it is published to (nuget.org, or a private feed). When using a private feed, add a `packageSourceMapping` entry in `nuget.config` so other packages still resolve from nuget.org.

## Testing The Package

The package itself needs tests. A `samples/MinimalConsumer/` project provides a tiny ASP.NET app with two routes (one anonymous, one `[Authorize]`) and a single-table EF Core schema. The package's test project consumes it as a `ProjectReference` and runs the screenshot suite against it in CI. This is both a test AND a living example — consumers can read it as reference implementation.

The test project specifically validates:
- Anonymous route renders + screenshot is written
- Authed route renders via `TestAuthHandler` + screenshot is written
- `IndexWriter` produces a well-formed `index.md`
- `ConsoleFilter.IsIgnorableNetworkError` returns true for known network error patterns and false for real JS errors
- Bootstrap migration runs before host startup

The test project does NOT validate against real-world failures (rolling the package out onto a full production app is the integration test). It validates that the package's contracts hold for a clean consumer.

## Migration Plan

1. **Prove the pattern** inline on a real production app first, so there is a working reference implementation in CI before extraction begins.
2. **Create the package repo** (this spec's home).
3. **Implement the package** by lifting the proven screenshot-test code out of the reference app into `src/Surfshack.Screenshots.Testing/`, generalizing types and removing project-specific code as it goes. Estimated 1-2 days of focused work given the prior proof.
4. **Stand up a Playwright-capable CI image** (or adopt Microsoft's official one). ~30 min.
5. **Set up CI** in `.gitlab-ci.yml` to build, test, and publish the NuGet package on `main` commits.
6. **Port a small app** as the second consumer — the smallest available .NET app is the lowest-risk validation that the abstraction holds. Estimated 1-2 hours per project after the package is stable.
7. **Port a UI-iteration-heavy app** third — the most likely day-to-day beneficiary.
8. **Refactor the original reference app** to use the package instead of its inlined copy. This step proves the abstraction can replace the original verbatim, and shrinks the reference app's screenshot test code by ~80%.
9. **Backfill remaining apps** as needed.

Each step is independently shippable and reversible.

## Open Questions

- **xUnit `[Theory]` discovery on generic base classes.** Need to confirm the consumer-thin-wrapper pattern actually works with xUnit 2.9.x. If not, fall back to source generators or accept the boilerplate.
- **Multiple test users per fixture.** Current `AuthedContextAsync(viewport, userId)` shape supports it, but `IScreenshotSeeder` has no notion of "create N test users." First consumer needing this drives the design.
- **Non-EF-Core projects.** The fixture's `TDbContext` constraint excludes apps that use Dapper, NHibernate, or no DB at all. A future `ScreenshotFixtureBaseNoDb<TFactory>` variant could lift the constraint, or we could make `TDbContext` optional via a marker type. Solve when we have a real consumer that needs it.
- **Slot for non-Identity auth schemes.** Some apps may use JWT, OAuth, or no auth. `TestAuthHandler` is hardcoded to inject one user via header. A future `IScreenshotAuth` extensibility point could decouple, but YAGNI until a real consumer needs JWT mocking.
- **Visual regression** is explicitly out of scope, but if/when it lands, it should be a separate `Surfshack.Screenshots.Diffing` package that consumes this one's output. Don't let that future need bleed into this package's design.

## Risks

- **WAF behavior drift across .NET versions.** The dual-host pattern depends on `IHostBuilder.Build()` being callable twice and accumulating config. If Microsoft changes this in .NET 11+, the package breaks. Mitigation: cross-version test in CI on each .NET preview, and pin the `Microsoft.AspNetCore.Mvc.Testing` version in the package's csproj.
- **`AppContext.BaseDirectory` path drift.** Screenshot output goes to `bin/{config}/{tfm}/TestResults/screenshots/`. If consumer projects move test output via `OutputPath` overrides, the artifact path in CI breaks. Mitigation: document the convention prominently in `README.md` and let consumers override `ScreenshotRoot` via a virtual property.
- **Race conditions across xUnit collections.** If a consumer puts the screenshot fixture in a collection that runs in parallel with another collection that also touches the same DB, the bootstrap-then-host ordering can interleave. Mitigation: document that the screenshot collection should be the only collection touching its database.

## Out of Scope (for follow-up specs)

- Visual regression diffing (separate package)
- Cross-browser support
- Mobile device emulation beyond the desktop/mobile viewports
- Screenshot upload to external storage (we only emit to artifact path)
- Test result reporting integrations beyond JUnit XML (already supported)
- Localized / RTL / theme-variant captures
- CI templates for non-GitLab platforms (GitHub Actions, etc.)
