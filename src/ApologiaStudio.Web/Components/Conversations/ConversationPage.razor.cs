using ApologiaStudio.Application.Agents.Settings;
using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;
using Microsoft.AspNetCore.Components;

namespace ApologiaStudio.Web.Components.Conversations;

public partial class ConversationPage
{
    [Parameter]
    public Conversation? Conversation { get; set; }

    [Parameter, EditorRequired]
    public IReadOnlyDictionary<AgentId, AgentSettingsSnapshot> AgentSettings { get; set; } =
        new Dictionary<AgentId, AgentSettingsSnapshot>();

    [Parameter]
    public string StreamingText { get; set; } = string.Empty;

    [Parameter]
    public string? ActiveAgentName { get; set; }

    [Parameter]
    public AgentId? ActiveAgentId { get; set; }

    [Parameter]
    public ApplicationLanguage Language { get; set; } =
        UserPreferences.DefaultInterfaceLanguage;

    [Parameter]
    public ApplicationLanguage TheologicalLanguage { get; set; } =
        UserPreferences.DefaultInterfaceLanguage;

    [Parameter, EditorRequired]
    public string DateFormat { get; set; } = string.Empty;

    [Parameter, EditorRequired]
    public string TimeFormat { get; set; } = string.Empty;

    [Parameter]
    public bool IsRenaming { get; set; }

    [Parameter]
    public bool IsSending { get; set; }

    [Parameter]
    public string RenameDraft { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> RenameDraftChanged { get; set; }

    [Parameter]
    public string Draft { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> DraftChanged { get; set; }

    [Parameter]
    public string SelectedAgentSlug { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> SelectedAgentSlugChanged { get; set; }

    [Parameter]
    public ComposerEnterBehavior EnterBehavior { get; set; } =
        UserPreferences.DefaultEnterBehavior;

    [Parameter]
    public string? RoutingReason { get; set; }

    [Parameter]
    public string? ErrorMessage { get; set; }

    [Parameter]
    public EventCallback OnCreateConversation { get; set; }

    [Parameter]
    public EventCallback OnRenameConversation { get; set; }

    [Parameter]
    public EventCallback OnSend { get; set; }

    private ConversationThread? _conversationThread;

    private bool CanRename =>
        Conversation is not null &&
        !IsRenaming &&
        !IsSending &&
        !string.IsNullOrWhiteSpace(RenameDraft) &&
        !string.Equals(
            RenameDraft.Trim(),
            Conversation.Title,
            StringComparison.Ordinal);

    private string ConversationTitleLabel =>
        Ui(
            "Titre de la conversation",
            "Conversation title");

    public void RequestScrollToLatestIfFollowing()
    {
        _conversationThread?.RequestScrollToLatestIfFollowing();
    }

    private Task HandleCreateConversationAsync()
    {
        return OnCreateConversation.InvokeAsync();
    }

    private Task HandleRenameDraftChangedAsync(
        ChangeEventArgs eventArgs)
    {
        return RenameDraftChanged.InvokeAsync(
            eventArgs.Value?.ToString() ?? string.Empty);
    }

    private Task HandleRenameConversationAsync()
    {
        return OnRenameConversation.InvokeAsync();
    }

    private Task HandleDraftChangedAsync(string draft)
    {
        return DraftChanged.InvokeAsync(draft);
    }

    private Task HandleSelectedAgentSlugChangedAsync(
        string selectedAgentSlug)
    {
        return SelectedAgentSlugChanged.InvokeAsync(selectedAgentSlug);
    }

    private Task HandleSendAsync()
    {
        return OnSend.InvokeAsync();
    }

    private Task HandleHistoricalSuggestionAsync()
    {
        var draft =
            TheologicalLanguage == ApplicationLanguage.English
                ? "When does the primacy of the Bishop of Rome " +
                  "first appear historically?"
                : "À quelle époque apparaît historiquement " +
                  "la primauté de l’évêque de Rome ?";

        return DraftChanged.InvokeAsync(draft);
    }

    private Task HandleApologeticSuggestionAsync()
    {
        var draft =
            TheologicalLanguage == ApplicationLanguage.English
                ? "How can the resurrection be defended " +
                  "against an atheist objection?"
                : "Comment défendre la résurrection " +
                  "face à une objection athée ?";

        return DraftChanged.InvokeAsync(draft);
    }

    private string Ui(string french, string english)
    {
        return Language == ApplicationLanguage.English
            ? english
            : french;
    }
}
