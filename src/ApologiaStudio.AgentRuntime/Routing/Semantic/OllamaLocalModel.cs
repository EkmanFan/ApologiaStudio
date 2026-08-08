namespace ApologiaStudio.AgentRuntime.Routing.Semantic;

public sealed record OllamaLocalModel(
    string Name,
    string? Family,
    string? ParameterSize,
    string? QuantizationLevel)
{
    public string DisplayName
    {
        get
        {
            var details = new[]
                {
                    ParameterSize,
                    QuantizationLevel
                }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            return details.Length == 0
                ? Name
                : $"{Name} — {string.Join(" / ", details)}";
        }
    }
}
