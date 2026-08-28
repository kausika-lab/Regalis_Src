namespace Regalis.IntegrationTests.Infrastructure;

/// <summary>One shared API host + test database for every integration test class.</summary>
[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<RegalisApiFactory>
{
    public const string Name = "regalis-api";
}
