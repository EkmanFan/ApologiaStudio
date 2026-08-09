using ApologiaStudio.Application.Agents.Settings;
using ApologiaStudio.Domain.Users;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ApologiaStudio.Web.Components.Conversations;

public partial class ConversationComposer : IAsyncDisposable
{
    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

    [Parameter]
    public IEnumerable<AgentSettingsSnapshot> Agents { get; set; } =
        Array.Empty<AgentSettingsSnapshot>();

    [Parameter]
    public string Draft { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> DraftChanged { get; set; }

    [Parameter]
    public string SelectedAgentSlug { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> SelectedAgentSlugChanged { get; set; }

    [Parameter]
    public bool IsSending { get; set; }

    [Parameter]
    public ComposerEnterBehavior EnterBehavior { get; set; } =
        UserPreferences.DefaultEnterBehavior;

    [Parameter]
    public ApplicationLanguage Language { get; set; } =
        UserPreferences.DefaultInterfaceLanguage;

    [Parameter]
    public EventCallback OnSend { get; set; }

    private ElementReference _textArea;
    private ElementReference _sendButton;
    private bool? _registeredSendOnEnter;

    private IEnumerable<AgentSettingsSnapshot> OrderedAgents =>
        Agents
            .Where(candidate => candidate.IsEnabled)
            .OrderByDescending(candidate => candidate.IsBuiltIn)
            .ThenBy(candidate => candidate.DisplayName);

    private string QuestionPlaceholder =>
        Ui(
            "Posez votre question…",
            "Ask your question…");

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var sendOnEnter =
            EnterBehavior == ComposerEnterBehavior.SendMessage;

        if (_registeredSendOnEnter == sendOnEnter)
        {
            return;
        }

        await JsRuntime.InvokeVoidAsync(
            "apologiaStudio.registerComposerEnterBehavior",
            _textArea,
            _sendButton,
            sendOnEnter);

        _registeredSendOnEnter = sendOnEnter;
    }

    private Task HandleDraftChangedAsync(ChangeEventArgs eventArgs)
    {
        return DraftChanged.InvokeAsync(
            eventArgs.Value?.ToString() ?? string.Empty);
    }

    private Task HandleAgentSelectionChangedAsync(ChangeEventArgs eventArgs)
    {
        return SelectedAgentSlugChanged.InvokeAsync(
            eventArgs.Value?.ToString() ?? string.Empty);
    }

    private Task HandleSendAsync()
    {
        return OnSend.InvokeAsync();
    }

    private string Ui(string french, string english)
    {
        return Language == ApplicationLanguage.English
            ? english
            : french;
    }

    public async ValueTask DisposeAsync()
    {
        if (_registeredSendOnEnter is null)
        {
            return;
        }

        try
        {
            await JsRuntime.InvokeVoidAsync(
                "apologiaStudio.unregisterComposerEnterBehavior",
                _textArea);
        }
        catch (JSDisconnectedException)
        {
            // The browser has already disconnected.
        }
        catch (InvalidOperationException)
        {
            // JavaScript interop is unavailable during shutdown.
        }
    }
}
