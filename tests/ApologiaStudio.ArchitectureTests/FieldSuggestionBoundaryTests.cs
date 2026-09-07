using System.Reflection;
using ApologiaStudio.Application.Abstractions.FieldSuggestions;

namespace ApologiaStudio.ArchitectureTests;

/// <summary>
/// The technology boundary of the field-suggestion capability.
/// </summary>
/// <remarks>
/// The point of a generic capability is that the application never learns which
/// machine produced a suggestion. These assert that structurally rather than by
/// convention: if an encoder, an LLM runtime, a bibliographic authority or a
/// persistence type ever reaches these contracts, the boundary is gone and the
/// next model change becomes an application change.
/// </remarks>
public sealed class FieldSuggestionBoundaryTests
{
    #region Variables and Constants

    private const string Namespace =
        "ApologiaStudio.Application.Abstractions.FieldSuggestions";

    private static readonly Type[] Contracts =
        typeof(IFieldSuggestionProvider).Assembly
            .GetTypes()
            .Where(x => x.Namespace == Namespace)
            .ToArray();

    #endregion

    #region Methods

    [Fact]
    public void The_capability_declares_the_expected_contracts()
    {
        Assert.Equal(
            [
                "ApologiaFieldId",
                "FieldSuggestion",
                "FieldSuggestionBatch",
                "FieldSuggestionContractException",
                "FieldSuggestionEvidence",
                "FieldSuggestionProvenance",
                "FieldSuggestionRequest",
                "FieldSuggestionResult",
                "FieldSuggestionStatus",
                "IFieldSuggestionProvider",
                "UnavailableFieldSuggestionProvider"
            ],
            Contracts
                .Where(x => x.IsPublic)
                .Select(x => x.Name)
                .OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void No_runtime_technology_reaches_the_contracts()
    {
        foreach (var type in Contracts)
        {
            foreach (var referenced in ReferencedTypes(type))
            {
                var name = referenced.Namespace ?? referenced.Name;

                Assert.True(
                    name.StartsWith("System", StringComparison.Ordinal) ||
                    string.Equals(name, Namespace, StringComparison.Ordinal),
                    $"{type.Name} reaches '{referenced.FullName}', which is " +
                    "outside the field-suggestion contracts.");
            }
        }
    }

    [Fact]
    public void No_forbidden_technology_is_named_anywhere_in_the_capability()
    {
        // A belt-and-braces check on names, so a string, a constant or a nested
        // helper cannot smuggle a technology in either.
        string[] forbidden =
        [
            "Ollama", "Xlm", "XLMR", "DeBerta", "Onnx", "Torch", "Python",
            "Cuda", "Rocm", "OpenVino", "Tensor", "Lcgft", "Authority",
            "DocumentProcessing", "EntityFramework", "Npgsql", "DbContext"
        ];

        var names = Contracts
            .SelectMany(x => x
                .GetMembers(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly)
                .Select(m => m.Name)
                .Append(x.Name))
            .ToList();

        foreach (var term in forbidden)
        {
            Assert.DoesNotContain(
                names,
                name => name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void The_capability_is_one_port_and_not_a_registry()
    {
        // One interface for every assisted field. No plugin container, no
        // service locator, no reflection-based dispatch.
        Assert.Single(Contracts.Where(x => x.IsInterface));

        var method = Assert.Single(
            typeof(IFieldSuggestionProvider).GetMethods());

        Assert.Equal("GetSuggestionsAsync", method.Name);
        Assert.Equal(
            [
                typeof(FieldSuggestionRequest),
                typeof(IReadOnlyCollection<ApologiaFieldId>),
                typeof(CancellationToken)
            ],
            method.GetParameters().Select(x => x.ParameterType));

        // Every asynchronous entry point takes a cancellation token.
        Assert.True(method.GetParameters()[^1].HasDefaultValue);
    }

    #endregion

    #region Methods Helpers

    /// <summary>
    /// Every type a contract exposes or holds: parameters, returns, properties,
    /// fields and generic arguments.
    /// </summary>
    private static IEnumerable<Type> ReferencedTypes(Type type)
    {
        const BindingFlags Flags =
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.DeclaredOnly;

        var candidates = new List<Type>();

        foreach (var method in type.GetMethods(Flags))
        {
            candidates.Add(method.ReturnType);
            candidates.AddRange(method.GetParameters().Select(x => x.ParameterType));
        }

        foreach (var constructor in type.GetConstructors(Flags))
        {
            candidates.AddRange(
                constructor.GetParameters().Select(x => x.ParameterType));
        }

        candidates.AddRange(type.GetProperties(Flags).Select(x => x.PropertyType));
        candidates.AddRange(type.GetFields(Flags).Select(x => x.FieldType));

        return candidates.SelectMany(Unwrap).Distinct();
    }

    private static IEnumerable<Type> Unwrap(Type type)
    {
        var current = type.IsByRef || type.IsArray
            ? type.GetElementType()!
            : type;

        yield return current;

        if (current.IsGenericType)
        {
            foreach (var argument in current.GetGenericArguments()
                         .SelectMany(Unwrap))
            {
                yield return argument;
            }
        }
    }

    #endregion
}
