using GoSit.Domain.Common;
using NetArchTest.Rules;
using Xunit;

namespace GoSit.ArchitectureTests;

/// <summary>
/// Enforces the modular-monolith layering: Domain depends on nothing,
/// Application depends only on Domain, and only Api/Infrastructure may
/// reference EF Core or other infrastructure concerns.
/// </summary>
public class LayerDependencyTests
{
    private static readonly System.Reflection.Assembly DomainAssembly = typeof(Entity).Assembly;
    private static readonly System.Reflection.Assembly ApplicationAssembly = typeof(GoSit.Application.DependencyInjection).Assembly;
    private static readonly System.Reflection.Assembly InfrastructureAssembly = typeof(GoSit.Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Domain_does_not_depend_on_outer_layers()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("GoSit.Application", "GoSit.Infrastructure", "GoSit.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, FailureMessage(result));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_api()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("GoSit.Infrastructure", "GoSit.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, FailureMessage(result));
    }

    [Fact]
    public void Domain_does_not_depend_on_entity_framework()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, FailureMessage(result));
    }

    [Fact]
    public void Application_does_not_depend_on_entity_framework()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, FailureMessage(result));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_api()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("GoSit.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, FailureMessage(result));
    }

    private static string FailureMessage(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
