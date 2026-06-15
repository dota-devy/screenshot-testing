namespace Surfshack.Screenshots.Testing.Hosting;

/// <summary>
/// Helper that constructs a test factory and forces its host to start, retrying past the
/// known first-run race in the dual-host pattern.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="KestrelTestFactoryBase{TProgram}"/> builds the WAF <c>TestServer</c> host and a
/// separate Kestrel host. On the first <c>CreateClient()</c>, WAF reaches into
/// <c>TestServer.Application</c> to build its handler; intermittently — most often on a cold CI
/// run — that read lands before the TestServer's started state has propagated and throws
/// <see cref="InvalidOperationException"/> "The server has not been started...". The condition
/// clears on a fresh attempt, so we recreate the factory and retry a few times rather than
/// pushing that flakiness onto every consumer's pipeline.
/// </para>
/// </remarks>
internal static class FactoryStartup
{
    private const int MaxAttempts = 3;

    /// <summary>
    /// Create a factory via <paramref name="create"/> and trigger host startup (via
    /// <c>CreateClient()</c>), retrying the known "server has not been started" race up to
    /// <see cref="MaxAttempts"/> times. A failed attempt's factory is disposed before retrying.
    /// </summary>
    public static async Task<TFactory> CreateStartedAsync<TFactory>(Func<TFactory> create)
        where TFactory : IKestrelTestFactory
    {
        for (var attempt = 1; ; attempt++)
        {
            var factory = create();
            try
            {
                // Triggers KestrelTestFactoryBase.CreateHost and the racy TestServer read.
                factory.CreateClient().Dispose();
                return factory;
            }
            catch (InvalidOperationException ex)
                when (attempt < MaxAttempts && ex.Message.Contains("has not been started", StringComparison.Ordinal))
            {
                await factory.DisposeAsync();
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt));
            }
        }
    }
}
