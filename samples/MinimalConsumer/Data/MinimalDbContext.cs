using Microsoft.EntityFrameworkCore;

namespace MinimalConsumer.Data;

public class MinimalDbContext(DbContextOptions<MinimalDbContext> options) : DbContext(options)
{
    public DbSet<Widget> Widgets => Set<Widget>();
}
