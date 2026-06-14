using Microsoft.AspNetCore.Hosting;
using Surfshack.Screenshots.Testing.Hosting;

namespace Surfshack.Screenshots.Testing.Tests.EndToEnd;

public class MinimalConsumerFactory(string connectionString) : KestrelTestFactoryBase<Program>
{
    protected override void ConfigureProject(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
    }
}
