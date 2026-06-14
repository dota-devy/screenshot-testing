using Microsoft.EntityFrameworkCore;
using MinimalConsumer.Data;
using Surfshack.Screenshots.Testing.Fixtures;

namespace Surfshack.Screenshots.Testing.Tests.EndToEnd;

public class MinimalConsumerFixture
    : ScreenshotFixtureBase<MinimalConsumerFactory, MinimalDbContext>
{
    protected override string ConnectionStringEnvVar => "MINIMAL_CONSUMER_TEST_DB";

    protected override MinimalDbContext CreateBootstrapContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<MinimalDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new MinimalDbContext(options);
    }

    protected override MinimalConsumerFactory CreateFactory(string connectionString)
        => new(connectionString);

    protected override IScreenshotSeeder Seeder { get; } = new MinimalConsumerSeeder();
}

[Xunit.CollectionDefinition(Name)]
public class MinimalConsumerCollection : Xunit.ICollectionFixture<MinimalConsumerFixture>
{
    public const string Name = "MinimalConsumerScreenshots";
}
