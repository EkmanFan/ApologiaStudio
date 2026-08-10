using System.Reflection;
using ApologiaStudio.AgentRuntime.Execution;
using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.ArchitectureTests;

public sealed class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly =
        typeof(UserId).Assembly;

    private static readonly Assembly ApplicationAssembly =
        typeof(IAgentRuntime).Assembly;

    private static readonly Assembly AgentRuntimeAssembly =
        typeof(OllamaAgentRuntime).Assembly;

    private static readonly Assembly InfrastructureAssembly =
        typeof(ApologiaStudio.Infrastructure.DependencyInjection).Assembly;

    private static readonly Assembly WebAssembly =
        typeof(ApologiaStudio.Web.DependencyInjection).Assembly;

    [Fact]
    public void Domain_Should_Not_Depend_On_Outer_Layers()
    {
        AssertDoesNotReference(
            DomainAssembly,
            ApplicationAssembly,
            AgentRuntimeAssembly,
            InfrastructureAssembly,
            WebAssembly);
    }

    [Fact]
    public void Application_Should_Only_Depend_Inward()
    {
        AssertReferences(
            ApplicationAssembly,
            DomainAssembly);

        AssertDoesNotReference(
            ApplicationAssembly,
            AgentRuntimeAssembly,
            InfrastructureAssembly,
            WebAssembly);
    }

    [Fact]
    public void AgentRuntime_Should_Not_Depend_On_Infrastructure_Or_Web()
    {
        AssertReferences(
            AgentRuntimeAssembly,
            ApplicationAssembly,
            DomainAssembly);

        AssertDoesNotReference(
            AgentRuntimeAssembly,
            InfrastructureAssembly,
            WebAssembly);
    }

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_AgentRuntime_Or_Web()
    {
        AssertReferences(
            InfrastructureAssembly,
            ApplicationAssembly,
            DomainAssembly);

        AssertDoesNotReference(
            InfrastructureAssembly,
            AgentRuntimeAssembly,
            WebAssembly);
    }

    private static void AssertReferences(
        Assembly source,
        params Assembly[] expectedDependencies)
    {
        var referencedAssemblies = GetReferencedAssemblyNames(source);

        foreach (var dependency in expectedDependencies)
        {
            Assert.Contains(
                dependency.GetName().Name!,
                referencedAssemblies);
        }
    }

    private static void AssertDoesNotReference(
        Assembly source,
        params Assembly[] forbiddenDependencies)
    {
        var referencedAssemblies = GetReferencedAssemblyNames(source);

        foreach (var dependency in forbiddenDependencies)
        {
            Assert.DoesNotContain(
                dependency.GetName().Name!,
                referencedAssemblies);
        }
    }

    private static HashSet<string> GetReferencedAssemblyNames(
        Assembly assembly)
    {
        return assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
    }
}
