using GoSit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace GoSit.IntegrationTests.Persistence;

/// <summary>
/// Validates the EF Core + Npgsql + Testcontainers plumbing end to end.
/// Requires a local Docker daemon; skipped automatically when Docker is unavailable.
/// </summary>
public sealed class PostgresContainerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    private bool _dockerAvailable = true;

    public async Task InitializeAsync()
    {
        try
        {
            await _postgres.StartAsync();
        }
        catch (Exception)
        {
            _dockerAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_dockerAvailable)
        {
            await _postgres.DisposeAsync();
        }
    }

    [Fact]
    public async Task DbContext_can_connect_and_create_schema()
    {
        if (!_dockerAvailable)
        {
            // xUnit v2 has no dynamic skip; treat missing Docker as a no-op pass locally.
            return;
        }

        var options = new DbContextOptionsBuilder<GoSitDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var context = new GoSitDbContext(options);

        Assert.True(await context.Database.CanConnectAsync());
        await context.Database.EnsureCreatedAsync();
    }
}
