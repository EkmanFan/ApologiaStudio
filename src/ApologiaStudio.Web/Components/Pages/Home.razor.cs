using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using static Microsoft.AspNetCore.Components.Web.RenderMode;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using ApologiaStudio.Web;
using ApologiaStudio.Web.Components;
using ApologiaStudio.Web.Components.Layout;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Application.Agents.Settings;
using ApologiaStudio.Application.Abstractions.BibleCorpora;
using ApologiaStudio.Application.BibleCorpora.Queries;
using ApologiaStudio.Application.BibleCorpora.Reader;
using ApologiaStudio.Application.Conversations.CreateConversation;
using ApologiaStudio.Application.Conversations.DeleteConversation;
using ApologiaStudio.Application.Conversations.GetConversation;
using ApologiaStudio.Application.Conversations.ListConversations;
using ApologiaStudio.Application.Conversations.MoveConversation;
using ApologiaStudio.Application.Conversations.RenameConversation;
using ApologiaStudio.Application.Conversations.RestoreConversation;
using ApologiaStudio.Application.Conversations.SendMessage;
using ApologiaStudio.Application.Navigation.ReorderPinnedItems;
using ApologiaStudio.Application.Navigation.ReorderProjects;
using ApologiaStudio.Application.Navigation.SetSidebarPin;
using ApologiaStudio.Application.Preferences;
using ApologiaStudio.Application.Navigation.GetSidebarNavigation;
using ApologiaStudio.Application.Projects.CreateProject;
using ApologiaStudio.Application.Projects.DeleteProject;
using ApologiaStudio.Application.Projects.RenameProject;
using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Navigation;
using ApologiaStudio.Domain.Projects;
using ApologiaStudio.Domain.Users;
using ApologiaStudio.Web.Components.Navigation;

namespace ApologiaStudio.Web.Components.Pages;

public partial class Home
{
    [Parameter]
    public Guid? ConversationIdValue { get; set; }

    private SidebarNavigationView _sidebarNavigation =
        SidebarNavigationView.Empty;

    private IReadOnlyList<BibleEditionSummary> _bibleEditions =
        Array.Empty<BibleEditionSummary>();

    private Conversation? _conversation;
    private ElementReference _conversationThread;
    private ElementReference _composerTextArea;
    private ElementReference _composerSendButton;
    private DotNetObjectReference<Home>? _dotNetReference;
    private string? _loadedRouteKey;
    private string? _preparedDraftRouteKey;
    private string _draft = string.Empty;
    private string _renameDraft = string.Empty;
    private string _selectedAgentSlug = string.Empty;
    private string _streamingText = string.Empty;
    private string? _activeAgentName;
    private AgentId? _activeAgentId;
    private IReadOnlyDictionary<AgentId, AgentSettingsSnapshot> _agentSettings =
        new Dictionary<AgentId, AgentSettingsSnapshot>();
    private string? _routingReason;
    private string? _errorMessage;
    private ApplicationLanguage _interfaceLanguage =
        UserPreferences.DefaultInterfaceLanguage;
    private ApplicationLanguage _theologicalLanguage =
        UserPreferences.DefaultInterfaceLanguage;
    private ComposerEnterBehavior _composerEnterBehavior =
        UserPreferences.DefaultEnterBehavior;
    private bool _isLoading = true;
    private bool _isSending;
    private bool _isCreatingConversation;
    private bool _isRenaming;
    private bool _isManagingSidebar;
    private bool _isSidebarOpen;
    private bool _isThreadNearBottom = true;
    private bool _showJumpToLatest;
    private bool _threadRegistered;
    private bool _composerEnterBehaviorRegistered;
    private bool _scrollThreadAfterRender;
    private bool _focusSidebarAfterRender;
    private bool _focusSidebarToggleAfterRender;

    private string ConversationTitleLabel =>
        Ui(
            "Titre de la conversation",
            "Conversation title");

    private string QuestionPlaceholder =>
        Ui(
            "Posez votre question…",
            "Ask your question…");

    protected override async Task OnParametersSetAsync()
    {
        var routeKey = GetRouteKey();

        if (routeKey == _loadedRouteKey)
        {
            return;
        }

        _loadedRouteKey = routeKey;
        _preparedDraftRouteKey = null;

        await LoadRouteAsync();
    }

    protected override async Task OnAfterRenderAsync(
        bool firstRender)
    {
        if (_focusSidebarAfterRender)
        {
            _focusSidebarAfterRender = false;

            await JsRuntime.InvokeVoidAsync(
                "apologiaStudio.focusElementById",
                "studio-sidebar-close");
        }

        if (_focusSidebarToggleAfterRender)
        {
            _focusSidebarToggleAfterRender = false;

            await JsRuntime.InvokeVoidAsync(
                "apologiaStudio.focusElementById",
                "studio-sidebar-toggle");
        }

        if (_conversation is null)
        {
            return;
        }

        if (!_threadRegistered)
        {
            _dotNetReference ??=
                DotNetObjectReference.Create(this);

            await JsRuntime.InvokeVoidAsync(
                "apologiaStudio.registerConversationThread",
                _conversationThread,
                _dotNetReference);

            _threadRegistered = true;
        }

        if (!_composerEnterBehaviorRegistered)
        {
            await JsRuntime.InvokeVoidAsync(
                "apologiaStudio.registerComposerEnterBehavior",
                _composerTextArea,
                _composerSendButton,
                _composerEnterBehavior ==
                    ComposerEnterBehavior.SendMessage);

            _composerEnterBehaviorRegistered = true;
        }

        if (_scrollThreadAfterRender)
        {
            _scrollThreadAfterRender = false;

            await JsRuntime.InvokeVoidAsync(
                "apologiaStudio.scrollConversationToEnd",
                _conversationThread,
                "auto");
        }
    }

    private async Task LoadRouteAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        _threadRegistered = false;
        _composerEnterBehaviorRegistered = false;
        _scrollThreadAfterRender = false;

        try
        {
            await using var scope =
                ServiceScopeFactory.CreateAsyncScope();

            var preferencesHandler =
                scope.ServiceProvider.GetRequiredService<
                    GetUserPreferencesHandler>();

            var preferences =
                await preferencesHandler.HandleAsync(
                    CancellationToken.None);

            _interfaceLanguage =
                preferences.InterfaceLanguage;

            _theologicalLanguage =
                preferences.EffectiveTheologicalLanguage;

            _composerEnterBehavior =
                preferences.EnterBehavior;

            await RefreshAgentSettingsAsync(
                scope.ServiceProvider,
                CancellationToken.None);

            var bibleRepository =
                scope.ServiceProvider.GetRequiredService<
                    IBibleCorpusQueryRepository>();

            _bibleEditions =
                await bibleRepository.ListActiveEditionsAsync(
                    CancellationToken.None);

            var navigationHandler =
                scope.ServiceProvider
                    .GetRequiredService<GetSidebarNavigationHandler>();

            _sidebarNavigation =
                await navigationHandler.HandleAsync(
                    CancellationToken.None);

            if (string.IsNullOrEmpty(GetRelativePath()))
            {
                if (_sidebarNavigation.DefaultConversationId is { }
                    defaultConversationId)
                {
                    NavigateToConversation(
                        defaultConversationId,
                        replace: true);

                    return;
                }

                var createHandler =
                    scope.ServiceProvider
                        .GetRequiredService<
                            CreateConversationHandler>();

                var createdConversation =
                    await createHandler.HandleAsync(
                        new CreateConversationCommand(
                            Ui(
                                "Nouvelle discussion",
                                "New conversation")),
                        CancellationToken.None);

                NavigateToConversation(
                    createdConversation.Id,
                    replace: true);

                return;
            }

            var conversationIdValue = ConversationIdValue
                ?? throw new InvalidOperationException(
                    "The conversation route is missing its identifier.");

            var getHandler =
                scope.ServiceProvider
                    .GetRequiredService<GetConversationHandler>();

            _conversation =
                await getHandler.HandleAsync(
                    new ConversationId(
                        conversationIdValue),
                    CancellationToken.None);

            _renameDraft =
                _conversation?.Title ?? string.Empty;

            _isThreadNearBottom = true;
            _showJumpToLatest = false;
            _scrollThreadAfterRender =
                _conversation is not null;

            await PrepareBibleDraftAsync(
                scope.ServiceProvider,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            _conversation = null;

            _errorMessage =
                Ui(
                    "La conversation n’a pas pu être chargée : ",
                    "The conversation could not be loaded: ") +
                exception.Message;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task CreateConversationAsync()
    {
        if (_isCreatingConversation)
        {
            return;
        }

        _isCreatingConversation = true;
        _errorMessage = null;

        try
        {
            await using var scope =
                ServiceScopeFactory.CreateAsyncScope();

            var handler =
                scope.ServiceProvider
                    .GetRequiredService<
                        CreateConversationHandler>();

            var conversation =
                await handler.HandleAsync(
                    new CreateConversationCommand(
                        Ui(
                            "Nouvelle discussion",
                            "New conversation")),
                    CancellationToken.None);

            NavigateToConversation(
                conversation.Id);
        }
        catch (Exception exception)
        {
            _errorMessage =
                Ui(
                    "La conversation n’a pas pu être créée : ",
                    "The conversation could not be created: ") +
                exception.Message;
        }
        finally
        {
            _isCreatingConversation = false;
        }
    }

    private async Task PrepareBibleDraftAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        if (_conversation is null ||
            _conversation.Messages.Count > 0 ||
            _preparedDraftRouteKey == _loadedRouteKey)
        {
            return;
        }

        var queryValues = QueryHelpers.ParseQuery(
            new Uri(NavigationManager.Uri).Query);

        var bibleParameterNames = new[]
        {
            "bibleEdition",
            "bibleBook",
            "bibleChapter",
            "bibleStart",
            "bibleEnd"
        };

        var hasAnyBibleParameter = bibleParameterNames.Any(
            queryValues.ContainsKey);

        if (!hasAnyBibleParameter)
        {
            return;
        }

        _preparedDraftRouteKey = _loadedRouteKey;

        var editionCode = queryValues
            .GetValueOrDefault("bibleEdition")
            .ToString();

        var bookCode = queryValues
            .GetValueOrDefault("bibleBook")
            .ToString();

        var startVerseLabel = queryValues
            .GetValueOrDefault("bibleStart")
            .ToString();

        var endVerseLabel = queryValues
            .GetValueOrDefault("bibleEnd")
            .ToString();

        if (string.IsNullOrWhiteSpace(editionCode) ||
            string.IsNullOrWhiteSpace(bookCode) ||
            !int.TryParse(
                queryValues
                    .GetValueOrDefault("bibleChapter")
                    .ToString(),
                out var chapterNumber) ||
            string.IsNullOrWhiteSpace(startVerseLabel))
        {
            _errorMessage = Ui(
                "La référence biblique transmise est incomplète.",
                "The supplied Bible reference is incomplete.");

            return;
        }

        var handler = serviceProvider.GetRequiredService<
            PrepareBibleDiscussionDraftHandler>();

        var draft = await handler.HandleAsync(
            new PrepareBibleDiscussionDraftQuery(
                editionCode,
                bookCode,
                chapterNumber,
                startVerseLabel,
                endVerseLabel,
                _theologicalLanguage),
            cancellationToken);

        if (draft is null)
        {
            _errorMessage = Ui(
                "La référence biblique n’existe pas dans le corpus actif.",
                "The Bible reference does not exist in the active corpus.");

            return;
        }

        _draft = draft.Prompt;
    }

    private async Task RenameConversationAsync()
    {
        if (_conversation is null ||
            _isRenaming ||
            string.IsNullOrWhiteSpace(_renameDraft))
        {
            return;
        }

        _isRenaming = true;
        _errorMessage = null;

        try
        {
            await using var scope =
                ServiceScopeFactory.CreateAsyncScope();

            var handler =
                scope.ServiceProvider
                    .GetRequiredService<
                        RenameConversationHandler>();

            await handler.HandleAsync(
                new RenameConversationCommand(
                    _conversation.Id,
                    _renameDraft.Trim()),
                CancellationToken.None);

            await LoadRouteAsync();
        }
        catch (Exception exception)
        {
            _errorMessage =
                Ui(
                    "La conversation n’a pas pu être renommée : ",
                    "The conversation could not be renamed: ") +
                exception.Message;
        }
        finally
        {
            _isRenaming = false;
        }
    }

    private async Task CreateProjectAsync(string name)
    {
        await ExecuteSidebarActionAsync(
            async (serviceProvider, cancellationToken) =>
            {
                var handler = serviceProvider.GetRequiredService<
                    CreateProjectHandler>();

                await handler.HandleAsync(
                    new CreateProjectCommand(name),
                    cancellationToken);
            },
            "Le projet n’a pas pu être créé : ",
            "The project could not be created: ");
    }

    private async Task RenameSidebarItemAsync(
        StudioSidebarRenameRequest request)
    {
        await ExecuteSidebarActionAsync(
            async (serviceProvider, cancellationToken) =>
            {
                if (request.IsProject)
                {
                    var handler = serviceProvider.GetRequiredService<
                        RenameProjectHandler>();

                    await handler.HandleAsync(
                        new RenameProjectCommand(
                            new ConversationProjectId(request.TargetId),
                            request.Name),
                        cancellationToken);

                    return;
                }

                var conversationHandler =
                    serviceProvider.GetRequiredService<
                        RenameConversationHandler>();

                await conversationHandler.HandleAsync(
                    new RenameConversationCommand(
                        new ConversationId(request.TargetId),
                        request.Name),
                    cancellationToken);
            },
            request.IsProject
                ? "Le projet n’a pas pu être renommé : "
                : "La discussion n’a pas pu être renommée : ",
            request.IsProject
                ? "The project could not be renamed: "
                : "The chat could not be renamed: ",
            reloadCurrentConversation:
                !request.IsProject &&
                _conversation?.Id.Value == request.TargetId);
    }

    private async Task SetSidebarPinAsync(
        StudioSidebarPinRequest request)
    {
        await ExecuteSidebarActionAsync(
            async (serviceProvider, cancellationToken) =>
            {
                var handler = serviceProvider.GetRequiredService<
                    SetSidebarPinHandler>();

                await handler.HandleAsync(
                    new SetSidebarPinCommand(
                        request.IsProject
                            ? SidebarPinTargetKind.Project
                            : SidebarPinTargetKind.Conversation,
                        request.TargetId,
                        request.IsPinned),
                    cancellationToken);
            },
            "L’épinglage n’a pas pu être modifié : ",
            "The pinned state could not be changed: ");
    }

    private async Task MoveConversationAsync(
        StudioSidebarMoveConversationRequest request)
    {
        await ExecuteSidebarActionAsync(
            async (serviceProvider, cancellationToken) =>
            {
                var handler = serviceProvider.GetRequiredService<
                    MoveConversationHandler>();

                await handler.HandleAsync(
                    new MoveConversationCommand(
                        new ConversationId(request.ConversationId),
                        request.ProjectId is { } projectId
                            ? new ConversationProjectId(projectId)
                            : null,
                        request.Position),
                    cancellationToken);
            },
            "La discussion n’a pas pu être déplacée : ",
            "The chat could not be moved: ",
            reloadCurrentConversation:
                _conversation?.Id.Value == request.ConversationId);
    }

    private async Task ReorderProjectsAsync(
        IReadOnlyList<Guid> orderedProjectIds)
    {
        await ExecuteSidebarActionAsync(
            async (serviceProvider, cancellationToken) =>
            {
                var handler = serviceProvider.GetRequiredService<
                    ReorderProjectsHandler>();

                await handler.HandleAsync(
                    new ReorderProjectsCommand(
                        orderedProjectIds
                            .Select(
                                id => new ConversationProjectId(id))
                            .ToArray()),
                    cancellationToken);
            },
            "L’ordre des projets n’a pas pu être enregistré : ",
            "The project order could not be saved: ");
    }

    private async Task ReorderPinnedItemsAsync(
        IReadOnlyList<Guid> orderedPinIds)
    {
        await ExecuteSidebarActionAsync(
            async (serviceProvider, cancellationToken) =>
            {
                var handler = serviceProvider.GetRequiredService<
                    ReorderPinnedItemsHandler>();

                await handler.HandleAsync(
                    new ReorderPinnedItemsCommand(
                        orderedPinIds
                            .Select(id => new SidebarPinId(id))
                            .ToArray()),
                    cancellationToken);
            },
            "L’ordre des éléments épinglés n’a pas pu être enregistré : ",
            "The pinned order could not be saved: ");
    }

    private async Task DeleteSidebarItemAsync(
        StudioSidebarDeleteRequest request)
    {
        var deletedCurrentConversation =
            !request.IsProject &&
            _conversation?.Id.Value == request.TargetId;

        var succeeded = await ExecuteSidebarActionAsync(
            async (serviceProvider, cancellationToken) =>
            {
                if (request.IsProject)
                {
                    var projectHandler =
                        serviceProvider.GetRequiredService<
                            DeleteProjectHandler>();

                    await projectHandler.HandleAsync(
                        new DeleteProjectCommand(
                            new ConversationProjectId(request.TargetId)),
                        cancellationToken);

                    return;
                }

                var conversationHandler =
                    serviceProvider.GetRequiredService<
                        DeleteConversationHandler>();

                await conversationHandler.HandleAsync(
                    new DeleteConversationCommand(
                        new ConversationId(request.TargetId)),
                    cancellationToken);
            },
            request.IsProject
                ? "Le projet n’a pas pu être supprimé : "
                : "La discussion n’a pas pu être placée dans la Corbeille : ",
            request.IsProject
                ? "The project could not be deleted: "
                : "The chat could not be moved to Trash: ",
            reloadCurrentConversation: request.IsProject);

        if (!succeeded || !deletedCurrentConversation)
        {
            return;
        }

        if (_sidebarNavigation.DefaultConversationId is { } nextId)
        {
            NavigateToConversation(nextId, replace: true);
            return;
        }

        NavigationManager.NavigateTo("/", replace: true);
    }

    private async Task RestoreConversationAsync(Guid conversationId)
    {
        var succeeded = await ExecuteSidebarActionAsync(
            async (serviceProvider, cancellationToken) =>
            {
                var handler = serviceProvider.GetRequiredService<
                    RestoreConversationHandler>();

                await handler.HandleAsync(
                    new RestoreConversationCommand(
                        new ConversationId(conversationId)),
                    cancellationToken);
            },
            "La discussion n’a pas pu être restaurée : ",
            "The chat could not be restored: ");

        if (succeeded)
        {
            NavigateToConversation(
                new ConversationId(conversationId));
        }
    }

    private async Task<bool> ExecuteSidebarActionAsync(
        Func<IServiceProvider, CancellationToken, Task> action,
        string frenchErrorPrefix,
        string englishErrorPrefix,
        bool reloadCurrentConversation = false)
    {
        if (_isManagingSidebar || _isSending)
        {
            return false;
        }

        _isManagingSidebar = true;
        _errorMessage = null;

        try
        {
            await using var scope =
                ServiceScopeFactory.CreateAsyncScope();

            await action(
                scope.ServiceProvider,
                CancellationToken.None);

            var navigationHandler =
                scope.ServiceProvider.GetRequiredService<
                    GetSidebarNavigationHandler>();

            _sidebarNavigation = await navigationHandler.HandleAsync(
                CancellationToken.None);

            if (reloadCurrentConversation && _conversation is not null)
            {
                var conversationHandler =
                    scope.ServiceProvider.GetRequiredService<
                        GetConversationHandler>();

                _conversation = await conversationHandler.HandleAsync(
                    _conversation.Id,
                    CancellationToken.None);

                _renameDraft = _conversation?.Title ?? string.Empty;
            }

            return true;
        }
        catch (Exception exception)
        {
            _errorMessage =
                Ui(frenchErrorPrefix, englishErrorPrefix) +
                exception.Message;

            return false;
        }
        finally
        {
            _isManagingSidebar = false;
        }
    }

    private void UseHistoricalSuggestion()
    {
        _draft =
            _theologicalLanguage == ApplicationLanguage.English
                ? "When does the primacy of the Bishop of Rome " +
                  "first appear historically?"
                : "À quelle époque apparaît historiquement " +
                  "la primauté de l’évêque de Rome ?";
    }

    private void UseApologeticSuggestion()
    {
        _draft =
            _theologicalLanguage == ApplicationLanguage.English
                ? "How can the resurrection be defended " +
                  "against an atheist objection?"
                : "Comment défendre la résurrection " +
                  "face à une objection athée ?";
    }

    private async Task SendAsync()
    {
        if (_conversation is null ||
            string.IsNullOrWhiteSpace(_draft) ||
            _isSending)
        {
            return;
        }

        var content = _draft.Trim();
        var conversationId = _conversation.Id;

        _draft = string.Empty;
        _streamingText = string.Empty;
        _routingReason = null;
        _errorMessage = null;
        _activeAgentName = null;
        _activeAgentId = null;
        _isSending = true;
        _scrollThreadAfterRender =
            _isThreadNearBottom;

        try
        {
            await using var scope =
                ServiceScopeFactory.CreateAsyncScope();

            await RefreshAgentSettingsAsync(
                scope.ServiceProvider,
                CancellationToken.None);

            var sendMessageHandler =
                scope.ServiceProvider
                    .GetRequiredService<SendMessageHandler>();

            var getConversationHandler =
                scope.ServiceProvider
                    .GetRequiredService<GetConversationHandler>();

            var command = new SendMessageCommand(
                conversationId,
                content,
                ResolveRequestedAgentId());

            await foreach (
                var agentEvent in
                sendMessageHandler.HandleAsync(
                    command,
                    CancellationToken.None))
            {
                switch (agentEvent)
                {
                    case AgentSelectedEvent selected:
                        _activeAgentId = selected.AgentId;
                        _activeAgentName =
                            GetAgentDisplayName(
                                selected.AgentId,
                                selected.AgentName);

                        _routingReason =
                            selected.Reason;

                        _conversation =
                            await getConversationHandler.HandleAsync(
                                conversationId,
                                CancellationToken.None)
                            ?? throw new InvalidOperationException(
                                "The conversation could not be reloaded.");

                        RequestScrollToLatestIfFollowing();

                        break;

                    case TextDeltaEvent delta:
                        _streamingText +=
                            delta.Content;

                        RequestScrollToLatestIfFollowing();

                        break;

                    case AgentTurnCompletedEvent:
                        _conversation =
                            await getConversationHandler.HandleAsync(
                                conversationId,
                                CancellationToken.None)
                            ?? throw new InvalidOperationException(
                                "The completed conversation could not be reloaded.");

                        _streamingText =
                            string.Empty;

                        RequestScrollToLatestIfFollowing();

                        break;
                }

                await InvokeAsync(
                    StateHasChanged);
            }
        }
        catch (Exception exception)
        {
            _errorMessage =
                Ui(
                    "La réponse n’a pas pu être produite : ",
                    "The response could not be generated: ") +
                exception.Message;
        }
        finally
        {
            _streamingText = string.Empty;
            _isSending = false;

            await InvokeAsync(
                StateHasChanged);
        }
    }

    private void NavigateToConversation(
        ConversationId conversationId,
        bool replace = false)
    {
        _isSidebarOpen = false;

        NavigationManager.NavigateTo(
            GetConversationUrl(conversationId),
            replace: replace);
    }

    private static string GetConversationUrl(
        ConversationId conversationId)
    {
        return $"/conversations/{conversationId.Value:D}";
    }

    private string GetRouteKey()
    {
        return NavigationManager.Uri;
    }

    private string GetRelativePath()
    {
        return NavigationManager
            .ToBaseRelativePath(NavigationManager.Uri)
            .Split('?', 2)[0]
            .Trim('/');
    }

    private IReadOnlyList<StudioSidebarBibleEdition>
        GetSidebarBibleEditions()
    {
        return _bibleEditions
            .Select(
                edition =>
                    new StudioSidebarBibleEdition(
                        edition.Code,
                        edition.DisplayName,
                        edition.LanguageTag,
                        $"/library/{Uri.EscapeDataString(edition.Code)}",
                        false))
            .ToArray();
    }

    private IReadOnlyList<StudioSidebarConversation>
        GetSidebarConversations()
    {
        return _sidebarNavigation.Chats
            .Select(
                conversation =>
                    new StudioSidebarConversation(
                        conversation.Id.Value,
                        null,
                        GetConversationUrl(conversation.Id),
                        conversation.Title,
                        _conversation?.Id == conversation.Id,
                        IsConversationPinned(conversation.Id.Value)))
            .ToArray();
    }

    private IReadOnlyList<StudioSidebarPinnedItem>
        GetSidebarPinnedItems()
    {
        return _sidebarNavigation.PinnedItems
            .Select(
                item =>
                {
                    var isProject =
                        item.TargetKind ==
                        SidebarPinTargetKind.Project;

                    var url = isProject
                        ? GetProjectAnchorUrl(item.TargetId)
                        : GetConversationUrl(
                            new ConversationId(item.TargetId));

                    return new StudioSidebarPinnedItem(
                        item.PinId.Value,
                        item.TargetId,
                        url,
                        item.Title,
                        isProject,
                        !isProject &&
                        _conversation?.Id.Value == item.TargetId);
                })
            .ToArray();
    }

    private IReadOnlyList<StudioSidebarProject>
        GetSidebarProjects()
    {
        return _sidebarNavigation.Projects
            .Select(
                project =>
                    new StudioSidebarProject(
                        project.Id.Value,
                        GetProjectAnchorId(project.Id.Value),
                        project.Name,
                        IsProjectPinned(project.Id.Value),
                        project.Conversations
                            .Select(
                                conversation =>
                                    new StudioSidebarConversation(
                                        conversation.Id.Value,
                                        project.Id.Value,
                                        GetConversationUrl(
                                            conversation.Id),
                                        conversation.Title,
                                        _conversation?.Id ==
                                            conversation.Id,
                                        IsConversationPinned(
                                            conversation.Id.Value)))
                            .ToArray()))
            .ToArray();
    }

    private IReadOnlyList<StudioSidebarDeletedConversation>
        GetSidebarDeletedConversations()
    {
        return _sidebarNavigation.DeletedChats
            .Select(
                conversation =>
                    new StudioSidebarDeletedConversation(
                        conversation.Id.Value,
                        conversation.Title,
                        conversation.DeletedAt))
            .ToArray();
    }

    private bool IsConversationPinned(Guid conversationId)
    {
        return _sidebarNavigation.PinnedItems.Any(
            item =>
                item.TargetKind == SidebarPinTargetKind.Conversation &&
                item.TargetId == conversationId);
    }

    private bool IsProjectPinned(Guid projectId)
    {
        return _sidebarNavigation.PinnedItems.Any(
            item =>
                item.TargetKind == SidebarPinTargetKind.Project &&
                item.TargetId == projectId);
    }

    private static string GetProjectAnchorUrl(Guid projectId)
    {
        return $"#{GetProjectAnchorId(projectId)}";
    }

    private static string GetProjectAnchorId(Guid projectId)
    {
        return $"sidebar-project-{projectId:N}";
    }

    private void OpenSidebar()
    {
        _isSidebarOpen = true;
        _focusSidebarAfterRender = true;
    }

    private void CloseSidebar()
    {
        var restoreFocus =
            _isSidebarOpen;

        _isSidebarOpen = false;

        if (restoreFocus)
        {
            _focusSidebarToggleAfterRender = true;
        }
    }

    private void HandleShellKeyDown(
        KeyboardEventArgs eventArgs)
    {
        if (_isSidebarOpen &&
            eventArgs.Key.Equals(
                "Escape",
                StringComparison.Ordinal))
        {
            CloseSidebar();
        }
    }

    private void RequestScrollToLatestIfFollowing()
    {
        if (_isThreadNearBottom)
        {
            _scrollThreadAfterRender = true;
        }
    }

    private async Task JumpToLatestAsync()
    {
        _isThreadNearBottom = true;
        _showJumpToLatest = false;

        await JsRuntime.InvokeVoidAsync(
            "apologiaStudio.scrollConversationToEnd",
            _conversationThread,
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
            (_conversation?.Messages.Count > 0 ||
             !string.IsNullOrEmpty(_streamingText));

        await InvokeAsync(
            StateHasChanged);
    }

    private AgentId? ResolveRequestedAgentId()
    {
        if (string.IsNullOrWhiteSpace(_selectedAgentSlug))
        {
            return null;
        }

        var selectedSettings =
            _agentSettings.Values.FirstOrDefault(
                candidate =>
                    candidate.IsEnabled &&
                    string.Equals(
                        candidate.Slug,
                        _selectedAgentSlug,
                        StringComparison.OrdinalIgnoreCase));

        return selectedSettings?.AgentId;
    }

    private static string GetMessageCssClass(
        ConversationMessage message)
    {
        return message.Role switch
        {
            MessageRole.User =>
                "message user",

            MessageRole.Agent =>
                "message agent",

            _ =>
                "message system"
        };
    }

    private string? GetMessageStyle(ConversationMessage message)
    {
        return message.Role == MessageRole.Agent
            ? GetAgentStyle(message.AgentId)
            : null;
    }

    private string GetMessageAuthor(
        ConversationMessage message)
    {
        if (message.Role == MessageRole.User)
        {
            return Ui("Vous", "You");
        }

        if (message.AgentId is { } agentId)
        {
            var persistedSettings = GetAgentSettings(agentId);
            if (persistedSettings is not null)
            {
                return persistedSettings.DisplayName;
            }

        }

        return Ui("Système", "System");
    }

    private string GetAgentDisplayName(
        AgentId agentId,
        string fallback)
    {
        return GetAgentSettings(agentId)?.DisplayName
               ?? fallback;
    }

    private string GetAgentAvatar(AgentId? agentId)
    {
        if (agentId is null)
        {
            return "AI";
        }

        return GetAgentSettings(agentId.Value)?.Avatar
               ?? "AI";
    }

    private string? GetAgentStyle(AgentId? agentId)
    {
        if (agentId is null)
        {
            return null;
        }

        var settings = GetAgentSettings(agentId.Value);
        if (settings is null)
        {
            return null;
        }

        var textColor = GetContrastTextColor(settings.BubbleColor);
        return $"--agent-bubble-color:{settings.BubbleColor};" +
               $"--agent-text-color:{textColor};";
    }

    private AgentSettingsSnapshot? GetAgentSettings(AgentId agentId)
    {
        return _agentSettings.TryGetValue(
            agentId,
            out var settings)
            ? settings
            : null;
    }

    private async Task RefreshAgentSettingsAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var handler =
            serviceProvider.GetRequiredService<
                GetAgentSettingsHandler>();
        var settings =
            await handler.HandleAsync(cancellationToken);

        _agentSettings = settings.ToDictionary(
            item => item.AgentId);
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

    private string Ui(
        string french,
        string english)
    {
        return _interfaceLanguage ==
                ApplicationLanguage.English
            ? english
            : french;
    }

    public async ValueTask DisposeAsync()
    {
        if (_composerEnterBehaviorRegistered)
        {
            try
            {
                await JsRuntime.InvokeVoidAsync(
                    "apologiaStudio.unregisterComposerEnterBehavior",
                    _composerTextArea);
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

        if (_threadRegistered)
        {
            try
            {
                await JsRuntime.InvokeVoidAsync(
                    "apologiaStudio.unregisterConversationThread",
                    _conversationThread);
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
