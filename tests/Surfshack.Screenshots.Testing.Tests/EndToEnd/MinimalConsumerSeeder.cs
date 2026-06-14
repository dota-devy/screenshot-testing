using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MinimalConsumer.Data;
using Surfshack.Screenshots.Testing.Fixtures;

namespace Surfshack.Screenshots.Testing.Tests.EndToEnd;

public class MinimalConsumerSeeder : IScreenshotSeeder
{
    public const string TestUserId = "test-user-1";
    public const int Widget1Id = 1;

    public async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MinimalDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();

        if (await db.Widgets.AnyAsync()) return;

        db.Widgets.Add(new Widget
        {
            Id = Widget1Id,
            Name = "Test Widget",
            Description = "A widget for screenshot tests.",
            Price = 9.99m,
            OwnerUserId = TestUserId,
        });
        await db.SaveChangesAsync();
    }
}
