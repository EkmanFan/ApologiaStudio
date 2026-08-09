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
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.Web.Components.Navigation;

public partial class StudioSidebar
{
    [Parameter, EditorRequired]
    public IReadOnlyList<StudioSidebarBibleEdition> BibleEditions { get; set; } =
        Array.Empty<StudioSidebarBibleEdition>();

    [Parameter, EditorRequired]
    public IReadOnlyList<StudioSidebarConversation> Conversations { get; set; } =
        Array.Empty<StudioSidebarConversation>();

    [Parameter]
    public IReadOnlyList<StudioSidebarPinnedItem> PinnedItems { get; set; } =
        Array.Empty<StudioSidebarPinnedItem>();

    [Parameter]
    public IReadOnlyList<StudioSidebarProject> Projects { get; set; } =
        Array.Empty<StudioSidebarProject>();

    [Parameter]
    public IReadOnlyList<StudioSidebarDeletedConversation> DeletedConversations { get; set; } =
        Array.Empty<StudioSidebarDeletedConversation>();

    [Parameter]
    public ApplicationLanguage Language { get; set; } =
        ApplicationLanguage.French;

    [Parameter]
    public bool IsOpen { get; set; }

    [Parameter]
    public bool IsCreatingConversation { get; set; }

    [Parameter]
    public bool IsBusy { get; set; }

    [Parameter]
    public EventCallback OnClose { get; set; }

    [Parameter]
    public EventCallback OnCreateConversation { get; set; }

    [Parameter]
    public EventCallback<string> OnCreateProject { get; set; }

    [Parameter]
    public EventCallback<StudioSidebarRenameRequest> OnRename { get; set; }

    [Parameter]
    public EventCallback<StudioSidebarPinRequest> OnSetPin { get; set; }

    [Parameter]
    public EventCallback<StudioSidebarMoveConversationRequest> OnMoveConversation { get; set; }

    [Parameter]
    public EventCallback<IReadOnlyList<Guid>> OnReorderProjects { get; set; }

    [Parameter]
    public EventCallback<IReadOnlyList<Guid>> OnReorderPinnedItems { get; set; }

    [Parameter]
    public EventCallback<StudioSidebarDeleteRequest> OnDelete { get; set; }

    [Parameter]
    public EventCallback<Guid> OnRestoreConversation { get; set; }

    private SidebarDialogKind _dialogKind;
    private SidebarDragKind _dragKind;
    private Guid _dialogTargetId;
    private Guid? _dialogSourceProjectId;
    private Guid _draggedId;
    private bool _dialogTargetIsProject;
    private string _dialogText = string.Empty;
    private string _dialogDestination = string.Empty;

    private Task CloseAsync()
    {
        return OnClose.InvokeAsync();
    }

    private Task HandlePinnedClickAsync(bool isProject)
    {
        return isProject
            ? Task.CompletedTask
            : OnClose.InvokeAsync();
    }

    private async Task CreateConversationAsync()
    {
        await OnCreateConversation.InvokeAsync();
        await OnClose.InvokeAsync();
    }

    private void OpenCreateProjectDialog()
    {
        _dialogKind = SidebarDialogKind.CreateProject;
        _dialogTargetIsProject = true;
        _dialogText = string.Empty;
    }

    private void OpenRenameDialog(
        Guid targetId,
        bool isProject,
        string name)
    {
        _dialogKind = SidebarDialogKind.Rename;
        _dialogTargetId = targetId;
        _dialogTargetIsProject = isProject;
        _dialogText = name;
    }

    private void OpenMoveDialog(
        StudioSidebarConversation conversation)
    {
        _dialogKind = SidebarDialogKind.MoveConversation;
        _dialogTargetId = conversation.Id;
        _dialogTargetIsProject = false;
        _dialogSourceProjectId = conversation.ProjectId;
        _dialogText = conversation.Title;

        _dialogDestination = conversation.ProjectId is not null
            ? string.Empty
            : Projects
                .First(project => project.Id != conversation.ProjectId)
                .Id
                .ToString("D");
    }

    private void OpenDeleteDialog(
        Guid targetId,
        bool isProject,
        string name)
    {
        _dialogKind = SidebarDialogKind.Delete;
        _dialogTargetId = targetId;
        _dialogTargetIsProject = isProject;
        _dialogText = name;
    }

    private void CloseDialog()
    {
        if (IsBusy)
        {
            return;
        }

        _dialogKind = SidebarDialogKind.None;
        _dialogTargetId = Guid.Empty;
        _dialogSourceProjectId = null;
        _dialogTargetIsProject = false;
        _dialogText = string.Empty;
        _dialogDestination = string.Empty;
    }

    private void HandleDialogKeyDown(KeyboardEventArgs eventArgs)
    {
        if (eventArgs.Key.Equals("Escape", StringComparison.Ordinal))
        {
            CloseDialog();
        }
    }

    private async Task SubmitTextDialogAsync()
    {
        var text = _dialogText.Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (_dialogKind == SidebarDialogKind.CreateProject)
        {
            await OnCreateProject.InvokeAsync(text);
        }
        else
        {
            await OnRename.InvokeAsync(
                new StudioSidebarRenameRequest(
                    _dialogTargetId,
                    _dialogTargetIsProject,
                    text));
        }

        CloseDialog();
    }

    private async Task SubmitMoveDialogAsync()
    {
        Guid? projectId = string.IsNullOrWhiteSpace(_dialogDestination)
            ? null
            : Guid.Parse(_dialogDestination);

        var position = projectId is null
            ? Conversations.Count
            : Projects.Single(project => project.Id == projectId.Value)
                .Conversations.Count;

        await MoveConversationAsync(
            _dialogTargetId,
            projectId,
            position);

        CloseDialog();
    }

    private async Task SubmitDeleteDialogAsync()
    {
        await OnDelete.InvokeAsync(
            new StudioSidebarDeleteRequest(
                _dialogTargetId,
                _dialogTargetIsProject));

        CloseDialog();
    }

    private Task SetPinAsync(
        Guid targetId,
        bool isProject,
        bool isPinned)
    {
        return OnSetPin.InvokeAsync(
            new StudioSidebarPinRequest(
                targetId,
                isProject,
                isPinned));
    }

    private Task MoveConversationAsync(
        Guid conversationId,
        Guid? projectId,
        int position)
    {
        return OnMoveConversation.InvokeAsync(
            new StudioSidebarMoveConversationRequest(
                conversationId,
                projectId,
                position));
    }

    private Task RestoreConversationAsync(Guid conversationId)
    {
        return OnRestoreConversation.InvokeAsync(conversationId);
    }

    private void BeginConversationDrag(Guid conversationId)
    {
        _dragKind = SidebarDragKind.Conversation;
        _draggedId = conversationId;
    }

    private void BeginProjectDrag(Guid projectId)
    {
        _dragKind = SidebarDragKind.Project;
        _draggedId = projectId;
    }

    private void BeginPinDrag(Guid pinId)
    {
        _dragKind = SidebarDragKind.Pin;
        _draggedId = pinId;
    }

    private void ClearDrag()
    {
        _dragKind = SidebarDragKind.None;
        _draggedId = Guid.Empty;
    }

    private async Task DropConversationAsync(
        Guid? projectId,
        int position)
    {
        if (_dragKind != SidebarDragKind.Conversation)
        {
            return;
        }

        var conversationId = _draggedId;
        ClearDrag();

        var sourceConversation = Conversations
            .Concat(
                Projects.SelectMany(project => project.Conversations))
            .SingleOrDefault(
                conversation => conversation.Id == conversationId);

        var destinationCount = projectId is null
            ? Conversations.Count
            : Projects.Single(project => project.Id == projectId.Value)
                .Conversations.Count;

        if (sourceConversation?.ProjectId == projectId)
        {
            destinationCount--;
        }

        position = Math.Clamp(position, 0, destinationCount);

        await MoveConversationAsync(
            conversationId,
            projectId,
            position);
    }

    private async Task DropProjectAsync(int position)
    {
        if (_dragKind != SidebarDragKind.Project)
        {
            return;
        }

        var projectId = _draggedId;
        ClearDrag();

        await ReorderProjectAsync(projectId, position);
    }

    private async Task ReorderProjectAsync(
        Guid projectId,
        int position)
    {
        var ids = Projects.Select(project => project.Id).ToList();
        var sourceIndex = ids.IndexOf(projectId);

        if (sourceIndex < 0)
        {
            return;
        }

        ids.RemoveAt(sourceIndex);
        ids.Insert(Math.Clamp(position, 0, ids.Count), projectId);

        await OnReorderProjects.InvokeAsync(ids);
    }

    private async Task DropPinAsync(int position)
    {
        if (_dragKind != SidebarDragKind.Pin)
        {
            return;
        }

        var pinId = _draggedId;
        ClearDrag();

        await ReorderPinAsync(pinId, position);
    }

    private async Task ReorderPinAsync(
        Guid pinId,
        int position)
    {
        var ids = PinnedItems.Select(item => item.PinId).ToList();
        var sourceIndex = ids.IndexOf(pinId);

        if (sourceIndex < 0)
        {
            return;
        }

        ids.RemoveAt(sourceIndex);
        ids.Insert(Math.Clamp(position, 0, ids.Count), pinId);

        await OnReorderPinnedItems.InvokeAsync(ids);
    }

    private bool HasAlternativeDestination(Guid? currentProjectId)
    {
        return currentProjectId is not null ||
               Projects.Any(project => project.Id != currentProjectId);
    }

    private string GetDialogTitle()
    {
        return _dialogKind switch
        {
            SidebarDialogKind.CreateProject =>
                Text("Créer un projet", "Create a project"),

            SidebarDialogKind.Rename =>
                Text("Renommer", "Rename"),

            SidebarDialogKind.MoveConversation =>
                Text("Déplacer la discussion", "Move chat"),

            SidebarDialogKind.Delete =>
                Text("Confirmer la suppression", "Confirm deletion"),

            _ => string.Empty
        };
    }

    private string GetLanguageLabel(string languageTag)
    {
        return languageTag.Equals("fr", StringComparison.OrdinalIgnoreCase)
            ? Text("Français", "French")
            : languageTag.Equals("en", StringComparison.OrdinalIgnoreCase)
                ? Text("Anglais", "English")
                : languageTag;
    }

    private string Text(string french, string english)
    {
        return Language == ApplicationLanguage.English
            ? english
            : french;
    }

    private enum SidebarDialogKind
    {
        None,
        CreateProject,
        Rename,
        MoveConversation,
        Delete
    }

    private enum SidebarDragKind
    {
        None,
        Conversation,
        Project,
        Pin
    }
}
