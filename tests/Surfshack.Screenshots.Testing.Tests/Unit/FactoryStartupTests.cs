using Surfshack.Screenshots.Testing.Hosting;
using Xunit;

namespace Surfshack.Screenshots.Testing.Tests.Unit;

public class FactoryStartupTests
{
    private const string RaceMessage =
        "The server has not been started or no web application was configured.";

    private sealed class FakeFactory(Func<HttpClient> createClient) : IKestrelTestFactory
    {
        public string ServerAddress => "http://127.0.0.1:0";
        public IServiceProvider Services => throw new NotSupportedException();
        public HttpClient CreateClient() => createClient();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Returns_on_first_success()
    {
        var creations = 0;
        var factory = await FactoryStartup.CreateStartedAsync(() =>
        {
            creations++;
            return new FakeFactory(() => new HttpClient());
        });

        Assert.NotNull(factory);
        Assert.Equal(1, creations);
    }

    [Fact]
    public async Task Retries_past_the_race_then_succeeds()
    {
        var attempt = 0;
        var factory = await FactoryStartup.CreateStartedAsync(() =>
        {
            attempt++;
            var thisAttempt = attempt;
            return new FakeFactory(() => thisAttempt < 3
                ? throw new InvalidOperationException(RaceMessage)
                : new HttpClient());
        });

        Assert.NotNull(factory);
        Assert.Equal(3, attempt);
    }

    [Fact]
    public async Task Gives_up_after_max_attempts()
    {
        var attempt = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FactoryStartup.CreateStartedAsync(() =>
            {
                attempt++;
                return new FakeFactory(() => throw new InvalidOperationException(RaceMessage));
            }));

        Assert.Equal(3, attempt);
    }

    [Fact]
    public async Task Does_not_retry_unrelated_errors()
    {
        var attempt = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FactoryStartup.CreateStartedAsync(() =>
            {
                attempt++;
                return new FakeFactory(() => throw new InvalidOperationException("some other configuration error"));
            }));

        Assert.Equal(1, attempt);
    }
}
