using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Conversations;

namespace ApologiaStudio.AgentRuntime.Execution;

public sealed record OllamaGenerationFirstTokenObservation(
    ConversationId ConversationId,
    AgentId AgentId,
    string Model,
    double TimeToFirstTokenMilliseconds);

public sealed record OllamaGenerationStartedObservation(
    ConversationId ConversationId,
    AgentId AgentId,
    string Model,
    int HistoryMessageCount,
    int MaximumOutputTokens);

public sealed record OllamaGenerationCompletedObservation(
    ConversationId ConversationId,
    AgentId AgentId,
    string Model,
    string DoneReason,
    int? PromptTokenCount,
    int? OutputTokenCount,
    long? TotalDurationNanoseconds,
    long? LoadDurationNanoseconds,
    long? PromptEvaluationDurationNanoseconds,
    long? EvaluationDurationNanoseconds);

public sealed record OllamaGenerationRejectedObservation(
    ConversationId ConversationId,
    AgentId AgentId,
    string Model,
    int GeneratedCharacterCount,
    int RepeatedPatternLength,
    int RepeatCount);

public sealed record OllamaHistoryMessageSkippedObservation(
    ConversationId ConversationId,
    MessageId MessageId,
    AgentId? AgentId,
    int CharacterCount,
    int RepeatedPatternLength,
    int RepeatCount);
