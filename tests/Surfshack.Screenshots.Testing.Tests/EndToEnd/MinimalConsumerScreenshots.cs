using Surfshack.Screenshots.Testing.Fixtures;
using Surfshack.Screenshots.Testing.Tests;
using Xunit;

namespace Surfshack.Screenshots.Testing.Tests.EndToEnd;

[Collection(MinimalConsumerCollection.Name)]
public class MinimalConsumerScreenshots(MinimalConsumerFixture fixture)
    : ScreenshotTestsBase<MinimalConsumerFixture>(fixture)
{
    private static readonly ViewportSpec[] _viewports =
    {
        ViewportSpec.Desktop,
        ViewportSpec.Mobile,
    };

    private static readonly RouteTestCase[] _routes = new[]
    {
        new RouteTestCase("widgets-index", Authed: false),
        new RouteTestCase("widgets-detail", Authed: false),
        new RouteTestCase("widgets-mine", Authed: true),
    };

    protected override IEnumerable<RouteTestCase> Routes => _routes;

    public static IEnumerable<object[]> Cases() => GetCases(_routes, _viewports);

    protected override string RouteUrlFor(string slug) => slug switch
    {
        "widgets-index"  => "/",
        "widgets-detail" => $"/widgets/{MinimalConsumerSeeder.Widget1Id}",
        "widgets-mine"   => "/widgets/mine",
        _ => throw new ArgumentOutOfRangeException(nameof(slug)),
    };

    protected override string AuthedUserId => MinimalConsumerSeeder.TestUserId;

    [Theory]
    [MemberData(nameof(Cases))]
    public Task CaptureRoute(ViewportSpec viewport, string slug, bool authed, string cartSessionCookie)
        => Capture(viewport, slug, authed, cartSessionCookie);
}
