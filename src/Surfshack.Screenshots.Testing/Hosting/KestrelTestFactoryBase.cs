using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Surfshack.Screenshots.Testing.Hosting;

/// <summary>
/// Non-generic surface of <see cref="KestrelTestFactoryBase{TProgram}"/> so fixtures
/// can hold a reference without binding to a specific <c>TProgram</c>.
/// </summary>
public interface IKestrelTestFactory
{
    string ServerAddress { get; }
    IServiceProvider Services { get; }
    HttpClient CreateClient();
    ValueTask DisposeAsync();
}

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> subclass that uses the dual-host
/// pattern to expose a real Kestrel-bound HTTP port for Playwright while keeping
/// WAF's <c>TestServer</c> machinery happy. Subclass and provide project-specific
/// configuration via <see cref="ConfigureProject"/>.
/// </summary>
/// <remarks>
/// <para>
/// The dual-host pattern is necessary because WAF unconditionally installs
/// <c>TestServer</c> in its internal <c>ConfigureWebHost</c> override (called after
/// the user's), so calling <c>UseKestrel()</c> alone has no effect on the resulting
/// host. To get a real HTTP-reachable port we build the same builder twice — once
/// for the WAF-owned TestServer host, then again after layering on <c>UseKestrel</c>
/// to produce a separate Kestrel host whose bound address we expose via
/// <see cref="ServerAddress"/>.
/// </para>
/// <para>
/// Both hosts run the application's startup pipeline, including any DB-touching
/// code in <c>Program.cs</c>. If your app seeds Identity roles or other unique-key
/// rows at startup, pre-create them in your fixture's <c>PreHostBootstrapAsync</c>
/// hook so both startups see the data already exists and skip the insert.
/// </para>
/// <para>
/// <c>CreateHost</c> and <c>ConfigureWebHost</c> are sealed — consumers cannot override
/// them, so everyone gets the same correct dual-host behavior with leak-safe disposal.
/// </para>
/// </remarks>
public abstract class KestrelTestFactoryBase<TProgram>
    : WebApplicationFactory<TProgram>, IKestrelTestFactory
    where TProgram : class
{
    private IHost? _kestrelHost;

    /// <summary>
    /// The actual Kestrel-bound URL (e.g. <c>http://127.0.0.1:35421</c>).
    /// Populated after the first call to <see cref="WebApplicationFactory{T}.CreateClient"/>.
    /// </summary>
    public string ServerAddress { get; private set; } = string.Empty;

    /// <summary>
    /// Subclass hook for project-specific configuration. Called from the package's
    /// sealed <see cref="ConfigureWebHost"/>. Use this to add <c>UseSetting</c> calls
    /// for connection strings, dummy API keys, etc., and any project-specific
    /// <c>ConfigureTestServices</c> additions beyond the auth scheme override.
    /// </summary>
    protected abstract void ConfigureProject(IWebHostBuilder builder);

    protected sealed override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ConfigureProject(builder);
        InstallTestAuthHandler(builder);
    }

    protected sealed override IHost CreateHost(IHostBuilder builder)
    {
        var testHost = builder.Build();

        try
        {
            builder.ConfigureWebHost(b =>
            {
                b.UseKestrel();
                b.UseUrls("http://127.0.0.1:0");
            });
            _kestrelHost = builder.Build();
            _kestrelHost.Start();

            var server = _kestrelHost.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>()
                ?? throw new InvalidOperationException("Kestrel did not expose IServerAddressesFeature.");
            ServerAddress = addresses.Addresses.FirstOrDefault()
                ?? throw new InvalidOperationException("Kestrel reported no bound addresses.");

            testHost.Start();
            return testHost;
        }
        catch
        {
            _kestrelHost?.Dispose();
            _kestrelHost = null;
            testHost.Dispose();
            throw;
        }
    }

    public override async ValueTask DisposeAsync()
    {
        if (_kestrelHost is not null)
        {
            await _kestrelHost.StopAsync();
            _kestrelHost.Dispose();
        }
        await base.DisposeAsync();
    }

    private static void InstallTestAuthHandler(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services
                .AddAuthentication(opt =>
                {
                    opt.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    opt.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    opt.DefaultScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { });

            // PostConfigure runs after every Configure (including AddIdentity's),
            // so this unconditionally wins the default-scheme assignment even when
            // the consumer's app uses ASP.NET Core Identity.
            services.PostConfigure<AuthenticationOptions>(opt =>
            {
                opt.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                opt.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                opt.DefaultSignInScheme = TestAuthHandler.SchemeName;
                opt.DefaultScheme = TestAuthHandler.SchemeName;
            });
        });
    }
}
