using ApologiaStudio.Application.Agents.Settings;
using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ConversationMessageModel = ApologiaStudio.Domain.Conversations.ConversationMessage;

namespace ApologiaStudio.Web.Components.Conversations;

public partial class ConversationThread : IAsyncDisposable
{
    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

    [Parameter]
    public IReadOnlyList<ConversationMessageModel> Messages { get; set; } =
        Array.Empty<ConversationMessageModel>();

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

    [Parameter, EditorRequired]
    public string DateFormat { get; set; } = string.Empty;

    [Parameter, EditorRequired]
    public string TimeFormat { get; set; } = string.Empty;

    [Parameter]
    public EventCallback OnHistoricalSuggestion { get; set; }

    [Parameter]
    public EventCallback OnApologeticSuggestion { get; set; }

    private ElementReference _threadElement;
    private DotNetObjectReference<ConversationThread>? _dotNetReference;
    private bool _threadRegistered;
    private bool _isThreadNearBottom = true;
    private bool _showJumpToLatest;
    private bool _scrollThreadAfterRender = true;

    private bool IsEnglish =>
        Language == ApplicationLanguage.English;

    private string ActiveAgentAvatar =>
        ActiveAgentId is { } agentId
            ? GetAgentSettings(agentId)?.Avatar ?? "AI"
            : "AI";

    private string? ActiveAgentStyle
    {
        get
        {
            if (ActiveAgentId is not { } agentId ||
                GetAgentSettings(agentId) is not { } settings)
            {
                return null;
            }

            var textColor = GetContrastTextColor(settings.BubbleColor);
            return $"--agent-bubble-color:{settings.BubbleColor};" +
                   $"--agent-text-color:{textColor};";
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_threadRegistered)
        {
            _dotNetReference ??=
                DotNetObjectReference.Create(this);

            await JsRuntime.InvokeVoidAsync(
                "apologiaStudio.registerConversationThread",
                _threadElement,
                _dotNetReference);

            _threadRegistered = true;
        }

        if (_scrollThreadAfterRender)
        {
            _scrollThreadAfterRender = false;

            await JsRuntime.InvokeVoidAsync(
                "apologiaStudio.scrollConversationToEnd",
                _threadElement,
                "auto");
        }
    }

    public void RequestScrollToLatestIfFollowing()
    {
        if (_isThreadNearBottom)
        {
            _scrollThreadAfterRender = true;
        }
    }

    private Task HandleHistoricalSuggestionAsync()
    {
        return OnHistoricalSuggestion.InvokeAsync();
    }

    private Task HandleApologeticSuggestionAsync()
    {
        return OnApologeticSuggestion.InvokeAsync();
    }

    private async Task JumpToLatestAsync()
    {
        _isThreadNearBottom = true;
        _showJumpToLatest = false;

        await JsRuntime.InvokeVoidAsync(
            "apologiaStudio.scrollConversationToEnd",
            _threadElement,
            "smooth");
    }

    [JSInvokable]
    public async Task SetConversationThreadNearBottom(
        bool isNearBottom)
    {
        if (_isThreadNearBottom == isNearBottom)
        {
            return;
        }

        _isThreadNearBottom = isNearBottom;
        _showJumpToLatest =
            !isNearBottom &&
            (Messages.Count > 0 ||
             !string.IsNullOrEmpty(StreamingText));

        await InvokeAsync(StateHasChanged);
    }

    private AgentSettingsSnapshot? GetAgentSettings(AgentId agentId)
    {
        return AgentSettings.TryGetValue(
            agentId,
            out var settings)
            ? settings
            : null;
    }

    private string Ui(string french, string english)
    {
        return Language == ApplicationLanguage.English
            ? english
            : french;
    }

    private static string GetContrastTextColor(string color)
    {
        if (color.Length != 7 ||
            color[0] != '#' ||
            !int.TryParse(
                color.AsSpan(1, 2),
                System.Globalization.NumberStyles.HexNumber,
                null,
                out var red) ||
            !int.TryParse(
                color.AsSpan(3, 2),
                System.Globalization.NumberStyles.HexNumber,
                null,
                out var green) ||
            !int.TryParse(
                color.AsSpan(5, 2),
                System.Globalization.NumberStyles.HexNumber,
                null,
                out var blue))
        {
            return "#252823";
        }

        var luminance =
            (0.299 * red + 0.587 * green + 0.114 * blue) / 255.0;

        return luminance > 0.58
            ? "#252823"
            : "#FFFFFF";
    }

    public async ValueTask DisposeAsync()
    {
        if (_threadRegistered)
        {
            try
            {
                await JsRuntime.InvokeVoidAsync(
                    "apologiaStudio.unregisterConversationThread",
                    _threadElement);
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

        _dotNetReference?.Dispose();
    }
}
