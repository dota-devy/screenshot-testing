using Surfshack.Screenshots.Testing.Fixtures;
using Surfshack.Screenshots.Testing.Tests;
using Xunit;

namespace Surfshack.Screenshots.Testing.Tests.EndToEnd.NoDbSample;

[Collection(NoDbSampleCollection.Name)]
public class NoDbSampleScreenshots(NoDbSampleFixture fixture)
    : ScreenshotTestsBase<NoDbSampleFixture>(fixture)
{
    private static readonly ViewportSpec[] _viewports =
    {
        ViewportSpec.Desktop,
        ViewportSpec.Wide,  // stresses the viewport generalization
    };

    private static readonly RouteCase[] _routes =
    {
        new("home", Authed: false),
        new("hello", Authed: false),
    };

    protected override IEnumerable<RouteCase> Routes => _routes;

    public static IEnumerable<object[]> Cases() => GetCases(_routes, _viewports);

    protected override string RouteUrlFor(string slug) => slug switch
    {
        "home" => "/",
        "hello" => "/hello",
        _ => throw new ArgumentOutOfRangeException(nameof(slug), slug, "Unknown slug"),
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public Task CaptureRoute(ViewportSpec viewport, string slug, bool authed, string cartSessionCookie)
        => Capture(viewport, slug, authed, cartSessionCookie);
}
