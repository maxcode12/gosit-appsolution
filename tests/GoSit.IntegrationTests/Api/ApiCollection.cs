using Xunit;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Shares one API host and PostgreSQL container across the API test classes.
/// Starting a container per class would dominate the suite's runtime.
/// </summary>
[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<GoSitApiFactory>;
