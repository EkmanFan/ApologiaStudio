using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ApologiaStudio.Infrastructure.Persistence.Configurations;

internal static class StronglyTypedIdConverters
{
    public static ValueConverter<ConversationId, Guid>
        ConversationIdConverter
    { get; } = new(
            id => id.Value,
            value => new ConversationId(value));

    public static ValueConverter<MessageId, Guid>
        MessageIdConverter
    { get; } = new(
            id => id.Value,
            value => new MessageId(value));

    public static ValueConverter<UserId, Guid>
        UserIdConverter
    { get; } = new(
            id => id.Value,
            value => new UserId(value));

    public static ValueConverter<AgentId?, Guid?>
        NullableAgentIdConverter
    { get; } = new(
            id =>
                id.HasValue
                    ? id.Value.Value
                    : null,
            value =>
                value.HasValue
                    ? new AgentId(value.Value)
                    : null);
}
