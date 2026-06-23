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

## The real superpower: agents that can see their own work

Increasingly the code that builds a web UI isn't written by a human — it's written by an AI
agent. And an agent has the same blind spot every developer has, only total: it emits a
thousand lines of HTML and CSS and has **no idea what the page actually looks like**.

This package closes that loop. Point an agent at it and the agent can build a page, capture a
real screenshot of it, *look at the image*, judge it, and iterate — fixing the crushed header,
the bruise-colored button, the card that never rendered — all without a human in the middle.
Deterministic, hermetic captures are exactly what makes this work: the same input yields the
same pixels, so an agent's "did my change help?" comparison is meaningful rather than noisy.

In other words, it gives AI-built websites a feedback loop: **build → see → judge → refine**,
run by the agent itself. (This very repository's consumer UI was refined that way.)

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
- **Hermetic rendering** — non-loopback requests are aborted, animations are disabled,
  scroll-reveal content is forced visible, and network/CORS console errors are filtered out,
  so screenshots are stable and complete across runs. Need a real font/icon CDN? Opt specific
  hosts back in with `AllowedExternalHosts`.
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
<PackageReference Include="Surfshack.Screenshots.Testing" Version="0.5.*" />
```

## Quick start (DB-backed)

You write **four small types** in your test project. Three are nearly boilerplate — a factory, a
seeder, and a fixture that wires them together — and the fourth, the test class, is the only one
with real decisions: it lists the pages to capture. Each step below shows exactly what to write
and what each piece does. The complete, runnable version lives in
[`samples/MinimalConsumer/`](samples/MinimalConsumer/) and is exercised by this repo's own test
project.

**1. A factory** — subclass `KestrelTestFactoryBase<Program>`. Its only job is to feed your app
the configuration it needs to boot under test (connection strings, fake API keys, feature flags):

```csharp
public sealed class MyAppTestFactory(string connectionString)
    : KestrelTestFactoryBase<Program>
{
    // Called by the base class while it builds the host. Push in whatever settings
    // your app reads at startup — here, just the database connection string.
    protected override void ConfigureProject(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Default", connectionString);
}
```

**2. A seeder** — implement `IScreenshotSeeder` to insert the data your pages need to render.
It runs once, after migrations and before any screenshot is taken:

```csharp
public sealed class MyAppSeeder : IScreenshotSeeder
{
    // 'services' is the *running app's* DI container, so resolve your real DbContext
    // (or repositories) and write whatever the routes you capture will display.
    public async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MyAppDbContext>();
        db.Widgets.Add(new Widget { Name = "Example" });
        await db.SaveChangesAsync();
    }
}
```

**3. A fixture** — subclass `ScreenshotFixtureBase<TFactory, TDbContext>`. This binds the factory
and seeder together and owns the browser. You supply three one-line overrides, plus the xUnit
collection definition that lets every test class share a single seeded app and browser:

```csharp
// xUnit boilerplate — declares the shared fixture for the collection. Copy as-is.
[CollectionDefinition(ScreenshotCollection.Name)]
public sealed class ScreenshotCollection : ICollectionFixture<MyAppScreenshotFixture>;

public sealed class MyAppScreenshotFixture : ScreenshotFixtureBase<MyAppTestFactory, MyAppDbContext>
{
    // Which seeder to run (from step 2).
    protected override IScreenshotSeeder Seeder => new MyAppSeeder();

    // How to build a DbContext for the one-time migrate step that runs before the app
    // starts. Use the same EF Core provider your app uses (SQLite shown; Npgsql etc. work).
    protected override MyAppDbContext CreateBootstrapContext(string connectionString) =>
        new(new DbContextOptionsBuilder<MyAppDbContext>().UseSqlite(connectionString).Options);

    // How to build your factory (from step 1).
    protected override MyAppTestFactory CreateFactory(string connectionString) =>
        new(connectionString);
}
```

**4. The test class** — this is the only class with real moving parts. Subclass
`ScreenshotTestsBase<TFixture>` and provide **three required members** (plus one more only if
you have logged-in pages). Start with the minimal version — two public pages, no auth:

```csharp
[Collection(ScreenshotCollection.Name)]
public sealed class MyAppScreenshots(MyAppScreenshotFixture fixture)
    : ScreenshotTestsBase<MyAppScreenshotFixture>(fixture)
{
    // The viewports every route is captured at. (More on viewports below.)
    private static readonly ViewportSpec[] _viewports = { ViewportSpec.Desktop, ViewportSpec.Mobile };

    // REQUIRED — the pages to screenshot, one RouteTestCase per page.
    // RouteTestCase(slug, Authed): 'slug' is a short id used as the PNG filename and the
    // test's display name; 'Authed: false' means capture the page logged-out.
    private static readonly RouteTestCase[] _routes =
    [
        new RouteTestCase("home",  Authed: false),
        new RouteTestCase("about", Authed: false),
    ];

    // REQUIRED — expose those same routes to the base class.
    protected override IEnumerable<RouteTestCase> Routes => _routes;

    // REQUIRED, but pure boilerplate — copy this line as-is. It's the data source
    // xUnit expands into one test per (viewport × route).
    public static IEnumerable<object[]> Cases() => GetCases(_routes, _viewports);

    // REQUIRED — map each route's slug to the URL path Playwright should visit.
    // This is where "home" becomes "/" and "about" becomes "/about".
    protected override string RouteUrlFor(string slug) => slug switch
    {
        "home"  => "/",
        "about" => "/about",
        _ => throw new ArgumentOutOfRangeException(nameof(slug)),
    };

    // REQUIRED, but pure boilerplate — the thin wrapper xUnit actually discovers as a
    // test. It just forwards each generated case to the base's Capture method.
    [Theory]
    [MemberData(nameof(Cases))]
    public Task CaptureRoute(ViewportSpec viewport, string slug, bool authed, string cartSessionCookie)
        => Capture(viewport, slug, authed, cartSessionCookie);
}
```

That's a complete, runnable suite. Here's every member, whether you must provide it, and what
value it takes:

| Member | Required? | What it is / what to put |
| --- | --- | --- |
| `_routes` + `Routes` | **Yes** | The pages to capture. `_routes` is your array of `RouteTestCase`s; `Routes` just exposes it to the base class. |
| `RouteUrlFor(slug)` | **Yes** | A `switch` (or any lookup) turning each route's `slug` into its URL path. Runs at test time, so it can reference IDs your seeder created (e.g. `$"/orders/{SeededOrderId}"`). |
| `Cases()` + the `[Theory]` wrapper | **Yes** | Unavoidable boilerplate — copy both verbatim. They hand xUnit one test per viewport × route. (Why it can't live in the base class: see the note below.) |
| `AuthedUserId` | Only if a route is `Authed: true` | The user id captured pages should be "logged in" as. Must match a user your seeder inserts. Defaults to empty. |
| `SessionCookieName` | Rarely | Only override if you use `RouteTestCase`'s optional cookie field (below). Defaults to `"SessionId"`. |

**What is a `RouteTestCase`?** A small record describing one page to capture:

```csharp
RouteTestCase(string Slug, bool Authed, string? CartSessionCookie = null)
```

- **`Slug`** — a short, filesystem-safe id (`"home"`, `"order-detail"`). Becomes the screenshot
  filename and the test's display name. This is the value `RouteUrlFor` receives.
- **`Authed`** — `false` captures the page logged-out; `true` captures it as a signed-in user
  (see below).
- **`CartSessionCookie`** *(optional, advanced)* — a session-cookie value to inject before
  navigating, for pages that need server-side session state (a populated cart, a multi-step
  form). Omit it unless you specifically need it; that's the `cartSessionCookie` parameter you
  see flowing through the `[Theory]` wrapper.

**Adding logged-in pages.** Mark the route `Authed: true` and set `AuthedUserId` to a user your
seeder created. The package signs every request as that user via a test auth header, so authed
pages render without a real login flow:

```csharp
private static readonly RouteTestCase[] _routes =
[
    new RouteTestCase("home",      Authed: false),
    new RouteTestCase("dashboard", Authed: true),   // captured as a signed-in user
];

protected override string AuthedUserId => "test-user-id";  // must match a user your seeder inserts
```

Screenshots are written to `bin/<config>/<tfm>/TestResults/screenshots/<viewport>/<slug>.png`,
with an `index.md` table of contents.

> Why the `Cases()` + `[Theory]`/`[MemberData]` boilerplate can't be hidden in the base class:
> xUnit's data-discovery doesn't traverse generic base classes, so the data source and the
> `[Theory]` attribute have to live in your assembly. It's the one piece of unavoidable
> ceremony — copy it and move on.

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

## Contributing

Contributions are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md) for development setup,
conventions, and how to open a merge request. Note that
[GitLab](https://gitlab.com/surfshack/screenshot-testing-oss) is the authoritative repository;
the GitHub copy is a read-only mirror.

## License

[MIT](LICENSE). Bundles the [JetBrains Mono](https://www.jetbrains.com/lp/mono/) font for
filmstrip labels under the SIL Open Font License 1.1 — see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
