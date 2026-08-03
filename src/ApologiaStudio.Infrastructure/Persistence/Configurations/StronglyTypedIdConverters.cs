using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.BibleCorpora;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Navigation;
using ApologiaStudio.Domain.Projects;
using ApologiaStudio.Domain.Users;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ApologiaStudio.Infrastructure.Persistence.Configurations;

internal static class StronglyTypedIdConverters
{
    public static ValueConverter<BibleCorpusVersionId, Guid>
        BibleCorpusVersionIdConverter
    { get; } = new(
            id => id.Value,
            value => new BibleCorpusVersionId(value));

    public static ValueConverter<BibleEditionCode, string>
        BibleEditionCodeConverter
    { get; } = new(
            code => code.Value,
            value => new BibleEditionCode(value));

    public static ValueConverter<UsfmBookCode, string>
        UsfmBookCodeConverter
    { get; } = new(
            code => code.Value,
            value => new UsfmBookCode(value));

    public static ValueConverter<Sha256Digest, string>
        Sha256DigestConverter
    { get; } = new(
            digest => digest.Value,
            value => new Sha256Digest(value));

    public static ValueConverter<ConversationId, Guid>
        ConversationIdConverter
    { get; } = new(
            id => id.Value,
            value => new ConversationId(value));

    public static ValueConverter<ConversationId?, Guid?>
        NullableConversationIdConverter
    { get; } = new(
            id =>
                id.HasValue
                    ? id.Value.Value
                    : null,
            value =>
                value.HasValue
                    ? new ConversationId(value.Value)
                    : null);

    public static ValueConverter<ConversationProjectId, Guid>
        ConversationProjectIdConverter
    { get; } = new(
            id => id.Value,
            value => new ConversationProjectId(value));

    public static ValueConverter<ConversationProjectId?, Guid?>
        NullableConversationProjectIdConverter
    { get; } = new(
            id =>
                id.HasValue
                    ? id.Value.Value
                    : null,
            value =>
                value.HasValue
                    ? new ConversationProjectId(value.Value)
                    : null);

    public static ValueConverter<SidebarPinId, Guid>
        SidebarPinIdConverter
    { get; } = new(
            id => id.Value,
            value => new SidebarPinId(value));

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
