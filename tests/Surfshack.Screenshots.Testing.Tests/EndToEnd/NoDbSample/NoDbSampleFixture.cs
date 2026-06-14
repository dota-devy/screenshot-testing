using Surfshack.Screenshots.Testing.Fixtures;

namespace Surfshack.Screenshots.Testing.Tests.EndToEnd.NoDbSample;

public class NoDbSampleFixture : ScreenshotFixtureBaseNoDb<NoDbSampleFactory>
{
    protected override NoDbSampleFactory CreateFactory() => new();
}
