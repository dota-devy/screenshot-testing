# Surfshack.Screenshots.Testing

A reusable [Playwright](https://playwright.dev/dotnet/) screenshot-testing pattern for
ASP.NET Core applications. Subclass a few base types, write a seeder for your domain model,
and you get a deterministic, CI-friendly UI screenshot suite in ~50–100 lines of
project-specific code — across as many viewports as you like, with optional animation
**filmstrip** capture for verifying CSS transitions.

## Why

Wiring Playwright up against a real ASP.NET Core app for screenshots is deceptively hard.
You have to run a real Kestrel server (Playwright needs a real URL, not `WebApplicationFactory`'s
in-memory `TestServer`), seed deterministic data, stub authentication, suppress animations,
keep the browser from reaching the public internet, and filter the console noise that would
otherwise fail every assertion. This package captures all of those decisions once, behind a
small, overridable surface, so each new consumer doesn't have to relearn them.

## Features

- **Dual-host factory** — runs your app under real Kestrel while keeping
  `WebApplicationFactory`'s configuration machinery, so Playwright drives a real bound URL.
- **DB-backed or no-DB** — `ScreenshotFixtureBase<TFactory, TDbContext>` migrates and seeds an
  EF Core database; `ScreenshotFixtureBaseNoDb<TFactory>` skips all of that for content-only
  sites and minimal APIs.
- **Deterministic seeding** — a one-method `IScreenshotSeeder` hook runs after migrations and
  before capture.
- **Header-based test auth** — `TestAuthHandler` authenticates requests from an `X-Test-User`
  header, so authed pages render without a real login flow.
- **Hermetic rendering** — non-loopback requests are aborted, animations are disabled, and
  network/CORS console errors are filtered out, so screenshots are stable across runs.
- **Any viewports** — built-in `ViewportSpec` presets (Desktop, Mobile, Tablet, Wide) plus
  inline custom sizes.
- **Filmstrip capture** — record N frames after a trigger and compose them into a single
  labeled strip image for reviewing animations/transitions.
- **Self-documenting output** — an `index.md` table of contents is written alongside the PNGs.

## Requirements

- .NET 10 SDK
- A Chromium browser for Playwright (installed automatically by Playwright, or pre-baked into
  your CI image — see [CI](#ci))

## Installation

```xml
<PackageReference Include="Surfshack.Screenshots.Testing" Version="0.3.*" />
```

## Quick start (DB-backed)

A consumer needs four small types. The complete, runnable version of the example below lives
in [`samples/MinimalConsumer/`](samples/MinimalConsumer/) and is exercised by this repo's own
test project.

**1. A factory** — subclass `KestrelTestFactoryBase<Program>` and supply project-specific
configuration:

```csharp
public sealed class MyAppTestFactory(string connectionString)
    : KestrelTestFactoryBase<Program>
{
    protected override void ConfigureProject(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Default", connectionString);
}
```

**2. A seeder** — implement `IScreenshotSeeder` against your domain model:

```csharp
public sealed class MyAppSeeder : IScreenshotSeeder
{
    public async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MyAppDbContext>();
        db.Widgets.Add(new Widget { Name = "Example" });
        await db.SaveChangesAsync();
    }
}
```

**3. A fixture** — subclass `ScreenshotFixtureBase<TFactory, TDbContext>` and tell it how to
build a bootstrap `DbContext` and the factory:

```csharp
[CollectionDefinition(ScreenshotCollection.Name)]
public sealed class ScreenshotCollection : ICollectionFixture<MyAppScreenshotFixture>;

public sealed class MyAppScreenshotFixture : ScreenshotFixtureBase<MyAppTestFactory, MyAppDbContext>
{
    protected override IScreenshotSeeder Seeder => new MyAppSeeder();

    protected override MyAppDbContext CreateBootstrapContext(string connectionString) =>
        new(new DbContextOptionsBuilder<MyAppDbContext>().UseSqlite(connectionString).Options);

    protected override MyAppTestFactory CreateFactory(string connectionString) =>
        new(connectionString);
}
```

**4. The tests** — subclass `ScreenshotTestsBase<TFixture>`, declare your routes and viewports,
and add the thin xUnit `[Theory]` wrapper:

```csharp
[Collection(ScreenshotCollection.Name)]
public sealed class MyAppScreenshots(MyAppScreenshotFixture fixture)
    : ScreenshotTestsBase<MyAppScreenshotFixture>(fixture)
{
    private static readonly ViewportSpec[] _viewports = { ViewportSpec.Desktop, ViewportSpec.Mobile };

    private static readonly RouteCase[] _routes =
    [
        new RouteCase("home", Authed: false),
        new RouteCase("dashboard", Authed: true),
    ];

    protected override IEnumerable<RouteCase> Routes => _routes;

    public static IEnumerable<object[]> Cases() => GetCases(_routes, _viewports);

    protected override string RouteUrlFor(string slug) => slug switch
    {
        "home"      => "/",
        "dashboard" => "/dashboard",
        _ => throw new ArgumentOutOfRangeException(nameof(slug)),
    };

    protected override string AuthedUserId => "test-user-id";

    [Theory]
    [MemberData(nameof(Cases))]
    public Task CaptureRoute(ViewportSpec viewport, string slug, bool authed, string cartSessionCookie)
        => Capture(viewport, slug, authed, cartSessionCookie);
}
```

Screenshots are written to `bin/<config>/<tfm>/TestResults/screenshots/<viewport>/<slug>.png`,
with an `index.md` table of contents.

> The 4-line `[Theory]`/`[MemberData]` wrapper is intentional: xUnit's data-discovery doesn't
> traverse generic base classes, so the attributes have to live in the consumer's assembly.

## No-DB consumers

For apps with no EF Core `DbContext` (content-only MVC sites, minimal APIs), subclass
`ScreenshotFixtureBaseNoDb<TFactory>` instead — no connection string, no migrations, no
`PreHostBootstrapAsync`:

```csharp
public sealed class MyAppScreenshotFixture : ScreenshotFixtureBaseNoDb<MyAppTestFactory>
{
    protected override MyAppTestFactory CreateFactory() => new();
}
```

Override `Seeder` only if you need to populate non-DB state (distributed cache, feature flags,
etc.). See [`samples/NoDbSample/`](samples/NoDbSample/) for a complete example.

## Viewports

`GetCases` takes a viewport list as its second argument. Use the presets or declare custom
sizes inline:

```csharp
private static readonly ViewportSpec[] _viewports =
{
    ViewportSpec.Desktop,        // 1440x900
    ViewportSpec.Mobile,         // 390x844
    ViewportSpec.Tablet,         // 768x1024
    ViewportSpec.Wide,           // 1920x1080
    new("ultrawide", 3840, 1600) // custom
};
```

A viewport's `Name` flows into screenshot paths (`TestResults/screenshots/<name>/<slug>.png`),
so keep it filesystem-safe — lowercase, hyphenated.

### Architecture

Layers generic over your `Program` and (optionally) `DbContext` types, so you override only the
part that varies:

```
Your test project                       Surfshack.Screenshots.Testing
─────────────────                       ─────────────────────────────
MyAppTestFactory        ─inherits─▶     KestrelTestFactoryBase<TProgram>            (dual-host + TestAuthHandler)
MyAppScreenshotFixture  ─inherits─▶     ScreenshotFixtureBase<TFactory,TDbContext>  (migrate → seed → browser)
                                        ScreenshotFixtureBaseNoDb<TFactory>         (no-DB variant)
MyAppScreenshots        ─inherits─▶     ScreenshotTestsBase<TFixture>               (Capture theory, routing, filters)
MyAppSeeder             ─implements─▶   IScreenshotSeeder
```

## Filmstrip capture

To verify an animation rather than a static page, `FilmstripCapture.CaptureAsync` snaps a
baseline frame, fires a trigger, then captures frames at a fixed cadence and composes them into
a single labeled strip image. Cadence, frame count, padding, colors, and labels are all tunable
via `FilmstripOptions`.

## CI

The tests are designed to run in CI (they need a Chromium browser and, for DB-backed fixtures,
a database). This repo's [`.gitlab-ci.yml`](.gitlab-ci.yml) is a working example using
Microsoft's official
[`mcr.microsoft.com/playwright/dotnet`](https://mcr.microsoft.com/product/playwright/dotnet/about)
image, which ships the .NET SDK plus Chromium and its system dependencies pre-installed. Keep
the image tag's Playwright version in sync with the `Microsoft.Playwright` package reference.

The same approach works on GitHub Actions or any other runner — run `dotnet test` inside (or
after installing) a Playwright-capable environment, and set the `TEST_DATABASE_CONNECTION_STRING`
environment variable your DB-backed fixture reads.

## Non-goals

- **Visual regression diffing** — this package emits PNGs; layer your own pixel diffing on top.
- **Cross-browser** — Chromium only.
- **Local-dev workflow** — designed for CI; wire up local runs yourself if you want them.

## License

[MIT](LICENSE). Bundles the [JetBrains Mono](https://www.jetbrains.com/lp/mono/) font for
filmstrip labels under the SIL Open Font License 1.1 — see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
