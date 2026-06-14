using Xunit;

namespace Surfshack.Screenshots.Testing.Tests.EndToEnd.NoDbSample;

[CollectionDefinition(Name)]
public class NoDbSampleCollection : ICollectionFixture<NoDbSampleFixture>
{
    public const string Name = "NoDb Sample Screenshots";
}
