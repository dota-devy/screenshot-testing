using Microsoft.AspNetCore.Hosting;
using Surfshack.Screenshots.Testing.Hosting;

namespace Surfshack.Screenshots.Testing.Tests.EndToEnd.NoDbSample;

public class NoDbSampleFactory : KestrelTestFactoryBase<global::NoDbSample.Program>
{
    protected override void ConfigureProject(IWebHostBuilder builder)
    {
        // No project-specific config needed for the no-DB sample —
        // the minimal-API app has no external dependencies to override.
    }
}
