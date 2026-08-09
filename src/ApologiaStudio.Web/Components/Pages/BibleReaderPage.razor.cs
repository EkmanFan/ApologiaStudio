using ApologiaStudio.Application.Abstractions.BibleCorpora;
using ApologiaStudio.Application.BibleCorpora.Queries;
using ApologiaStudio.Application.BibleCorpora.Reader;
using ApologiaStudio.Application.Conversations.CreateConversation;
using ApologiaStudio.Application.Conversations.DeleteConversation;
using ApologiaStudio.Application.Conversations.MoveConversation;
using ApologiaStudio.Application.Conversations.RenameConversation;
using ApologiaStudio.Application.Conversations.RestoreConversation;
using ApologiaStudio.Application.Navigation.GetSidebarNavigation;
using ApologiaStudio.Application.Navigation.ReorderPinnedItems;
using ApologiaStudio.Application.Navigation.ReorderProjects;
using ApologiaStudio.Application.Navigation.SetSidebarPin;
using ApologiaStudio.Application.Preferences;
using ApologiaStudio.Application.Projects.CreateProject;
using ApologiaStudio.Application.Projects.DeleteProject;
using ApologiaStudio.Application.Projects.RenameProject;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Navigation;
using ApologiaStudio.Domain.Projects;
using ApologiaStudio.Domain.Users;
using ApologiaStudio.Web.Components.BibleReader;
using ApologiaStudio.Web.Components.Navigation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace ApologiaStudio.Web.Components.Pages;

public partial class BibleReaderPage : IAsyncDisposable
{
    private readonly CancellationTokenSource _lifetimeCts = new();
    private CancellationTokenSource? _routeCts;

    [Inject]
    private IServiceScopeFactory ServiceScopeFactory { get; set; } = null!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = null!;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = null!;

    [Parameter]
    public string? LibraryEditionCode { get; set; }

    [Parameter]
    public string? LibraryBookCode { get; set; }

    [Parameter]
    public int? LibraryChapterNumber { get; set; }

    private SidebarNavigationView _sidebarNavigation =
        SidebarNavigationView.Empty;

    private IReadOnlyList<BibleEditionSummary> _bibleEditions =
        Array.Empty<BibleEditionSummary>();

    private BibleReaderView? _bibleReader;
    private string? _loadedRouteKey;
    private string? _errorMessage;
    private ApplicationLanguage _interfaceLanguage =
        UserPreferences.DefaultInterfaceLanguage;
    private bool _isLoading = true;
    private bool _isCreatingConversation;
    private bool _isManagingSidebar;
    private bool _isSidebarOpen;
    private bool _focusSidebarAfterRender;
    private bool _focusSidebarToggleAfterRender;

    protected override async Task OnParametersSetAsync()
    {
        var routeKey = NavigationManager.Uri;
        if (routeKey == _loadedRouteKey)
        {
            return;
        }

        _loadedRouteKey = routeKey;

        _routeCts?.Cancel();
        _routeCts?.Dispose();
        var routeCts = CancellationTokenSource.CreateLinkedTokenSource(
            _lifetimeCts.Token);
        _routeCts = routeCts;
        var routeToken = routeCts.Token;

        try
        {
            await LoadRouteAsync(routeToken);
        }
        catch (OperationCanceledException)
            when (routeToken.IsCancellationRequested)
        {
            // A newer route or component disposal superseded this load.
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            if (_focusSidebarAfterRender)
            {
                _focusSidebarAfterRender = false;
                await JsRuntime.InvokeVoidAsync(
                    "apologiaStudio.focusElementById",
                    _lifetimeCts.Token,
                    "studio-sidebar-close");
            }

            if (_focusSidebarToggleAfterRender)
            {
                _focusSidebarToggleAfterRender = false;
                await JsRuntime.InvokeVoidAsync(
                    "apologiaStudio.focusElementById",
                    _lifetimeCts.Token,
                    "studio-sidebar-toggle");
            }
        }
        catch (OperationCanceledException)
            when (_lifetimeCts.IsCancellationRequested)
        {
            // Component disposal cancels pending JavaScript interop.
        }
        catch (JSDisconnectedException)
        {
            // The browser circuit has already disconnected.
        }
    }

    private async Task LoadRouteAsync(CancellationToken cancellationToken)
    {
        _isLoading = true;
        _errorMessage = null;
        _bibleReader = null;

        try
        {
            await using var scope =
                ServiceScopeFactory.CreateAsyncScope();

            var preferencesHandler =
                scope.ServiceProvider.GetRequiredService<
                    GetUserPreferencesHandler>();

            var preferences = await preferencesHandler.HandleAsync(
                cancellationToken);

            _interfaceLanguage = preferences.InterfaceLanguage;

            var bibleRepository =
                scope.ServiceProvider.GetRequiredService<
                    IBibleCorpusQueryRepository>();

            _bibleEditions =
                await bibleRepository.ListActiveEditionsAsync(
                    cancellationToken);

            var navigationHandler =
                scope.ServiceProvider.GetRequiredService<
                    GetSidebarNavigationHandler>();

            _sidebarNavigation = await navigationHandler.HandleAsync(
                cancellationToken);

            var readerHandler =
                scope.ServiceProvider.GetRequiredService<
                    GetBibleReaderHandler>();

            _bibleReader = await readerHandler.HandleAsync(
                new GetBibleReaderQuery(
                    LibraryEditionCode ?? string.Empty,
                    LibraryBookCode,
                    LibraryChapterNumber),
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            _bibleReader = new BibleReaderView(
                BibleReaderStatus.CorpusUnavailable,
                _bibleEditions);
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _isLoading = false;
            }
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
                scope.ServiceProvider.GetRequiredService<
                    CreateConversationHandler>();

            var conversation = await handler.HandleAsync(
                new CreateConversationCommand(
                    Ui("Nouvelle discussion", "New conversation")),
                _lifetimeCts.Token);

            NavigateToConversation(conversation.Id);
        }
        catch (OperationCanceledException)
            when (_lifetimeCts.IsCancellationRequested)
        {
            // Navigation/disposal cancelled the operation.
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

    private async Task UseBibleSelectionAsync(
        BibleReaderSelection selection)
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
                scope.ServiceProvider.GetRequiredService<
                    CreateConversationHandler>();

            var conversation = await handler.HandleAsync(
                new CreateConversationCommand(
                    Ui("Étude biblique", "Bible study")),
                _lifetimeCts.Token);

            NavigationManager.NavigateTo(
                GetBibleDiscussionUrl(
                    conversation.Id,
                    selection));
        }
        catch (OperationCanceledException)
            when (_lifetimeCts.IsCancellationRequested)
        {
            // Navigation/disposal cancelled the operation.
        }
        catch (Exception exception)
        {
            _errorMessage =
                Ui(
                    "La discussion n’a pas pu être préparée : ",
                    "The chat could not be prepared: ") +
                exception.Message;
        }
        finally
        {
            _isCreatingConversation = false;
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
                    var projectHandler =
                        serviceProvider.GetRequiredService<
                            RenameProjectHandler>();

                    await projectHandler.HandleAsync(
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
                : "The chat could not be renamed: ");
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
            "The chat could not be moved: ");
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
                            .Select(id => new ConversationProjectId(id))
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
        await ExecuteSidebarActionAsync(
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
                : "The chat could not be moved to Trash: ");
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
            NavigateToConversation(new ConversationId(conversationId));
        }
    }

    private async Task<bool> ExecuteSidebarActionAsync(
        Func<IServiceProvider, CancellationToken, Task> action,
        string frenchErrorPrefix,
        string englishErrorPrefix)
    {
        if (_isManagingSidebar)
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
                _lifetimeCts.Token);

            var navigationHandler =
                scope.ServiceProvider.GetRequiredService<
                    GetSidebarNavigationHandler>();

            _sidebarNavigation = await navigationHandler.HandleAsync(
                _lifetimeCts.Token);

            return true;
        }
        catch (OperationCanceledException)
            when (_lifetimeCts.IsCancellationRequested)
        {
            return false;
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
                        edition.Code.Equals(
                            LibraryEditionCode,
                            StringComparison.OrdinalIgnoreCase)))
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
                        false,
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
                        item.TargetKind == SidebarPinTargetKind.Project;

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
                        false);
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
                                        false,
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

    private static string GetBibleDiscussionUrl(
        ConversationId conversationId,
        BibleReaderSelection selection)
    {
        return GetConversationUrl(conversationId) +
               $"?bibleEdition={Uri.EscapeDataString(selection.EditionCode)}" +
               $"&bibleBook={Uri.EscapeDataString(selection.BookCode)}" +
               $"&bibleChapter={selection.ChapterNumber}" +
               $"&bibleStart={Uri.EscapeDataString(selection.StartVerseLabel)}" +
               $"&bibleEnd={Uri.EscapeDataString(selection.EndVerseLabel)}";
    }

    private string GetPageTitle()
    {
        if (_bibleReader?.Chapter is { } chapter)
        {
            return $"{chapter.Book.DisplayName} " +
                   $"{chapter.ChapterNumber} — Apologia Studio";
        }

        return Ui(
            "Lecteur biblique — Apologia Studio",
            "Bible Reader — Apologia Studio");
    }

    private void OpenSidebar()
    {
        _isSidebarOpen = true;
        _focusSidebarAfterRender = true;
    }

    private void CloseSidebar()
    {
        var restoreFocus = _isSidebarOpen;

        _isSidebarOpen = false;
        if (restoreFocus)
        {
            _focusSidebarToggleAfterRender = true;
        }
    }

    private void HandleShellKeyDown(KeyboardEventArgs eventArgs)
    {
        if (_isSidebarOpen &&
            eventArgs.Key.Equals(
                "Escape",
                StringComparison.Ordinal))
        {
            CloseSidebar();
        }
    }

    private string Ui(string french, string english)
    {
        return _interfaceLanguage == ApplicationLanguage.English
            ? english
            : french;
    }

    public ValueTask DisposeAsync()
    {
        _routeCts?.Cancel();
        _routeCts?.Dispose();

        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();

        return ValueTask.CompletedTask;
    }
}
