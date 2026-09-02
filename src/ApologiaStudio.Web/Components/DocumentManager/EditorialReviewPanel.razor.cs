using ApologiaStudio.Application.Knowledge.DocumentProcessing;
using ApologiaStudio.Domain.Users;
using Microsoft.AspNetCore.Components;

namespace ApologiaStudio.Web.Components.DocumentManager;

public partial class EditorialReviewPanel
{
    [Inject]
    private IServiceScopeFactory ServiceScopeFactory { get; set; } = null!;

    [Parameter]
    public ApplicationLanguage Language { get; set; } = ApplicationLanguage.French;

    [Parameter]
    public bool SidebarIsOpen { get; set; }

    [Parameter]
    public EventCallback OnOpenSidebar { get; set; }

    private IReadOnlyList<DocumentManagerEditorialDraftSummary> _drafts =
        Array.Empty<DocumentManagerEditorialDraftSummary>();
    private DocumentManagerEditorialDraft? _selectedDraft;
    private EditorialForm _form = new();
    private DocumentManagerEditorialReviewAction? _pendingAction;
    private string _statusFilter = "all";
    private string? _loadError;
    private string? _message;
    private bool _messageIsError;
    private bool _isLoading = true;
    private bool _isSelecting;
    private bool _isSaving;

    private IReadOnlyList<DocumentManagerEditorialDraftSummary> FilteredDrafts =>
        _drafts
            .Where(MatchesFilter)
            .ToArray();

    private bool IsReadOnly =>
        _selectedDraft?.Status is
            DocumentManagerEditorialDraftStatus.Approved or
            DocumentManagerEditorialDraftStatus.Rejected;

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadError = null;

        try
        {
            await using var scope = ServiceScopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<
                ListDocumentManagerEditorialDraftsHandler>();
            _drafts = await handler.HandleAsync(CancellationToken.None);

            if (_drafts.Count > 0)
            {
                var selectedId = _selectedDraft?.Id;
                var selection = selectedId is not null &&
                                _drafts.Any(draft => draft.Id == selectedId)
                    ? selectedId.Value
                    : _drafts[0].Id;
                await LoadDraftAsync(scope.ServiceProvider, selection);
            }
            else
            {
                _selectedDraft = null;
            }
        }
        catch (Exception exception)
        {
            _loadError = exception.Message;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SelectDraftAsync(Guid draftId)
    {
        if (_selectedDraft?.Id == draftId || _isSelecting)
        {
            return;
        }

        _isSelecting = true;
        _message = null;
        _pendingAction = null;

        try
        {
            await using var scope = ServiceScopeFactory.CreateAsyncScope();
            await LoadDraftAsync(scope.ServiceProvider, draftId);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            _isSelecting = false;
        }
    }

    private async Task LoadDraftAsync(IServiceProvider services, Guid draftId)
    {
        var handler = services.GetRequiredService<
            GetDocumentManagerEditorialDraftHandler>();
        var draft = await handler.HandleAsync(draftId, CancellationToken.None);

        _selectedDraft = draft ?? throw new InvalidOperationException(
            Text("La fiche demandée n’existe plus.", "The requested record no longer exists."));
        _form = EditorialForm.FromDraft(_selectedDraft);
    }

    private Task SaveAsync() => ExecuteAsync(DocumentManagerEditorialReviewAction.Save);

    private void HandleContributorRoleChanged(ChangeEventArgs eventArgs)
    {
        _form.PrimaryContributorRole = eventArgs.Value?.ToString();
    }

    private void AskConfirmation(DocumentManagerEditorialReviewAction action)
    {
        _message = null;
        _pendingAction = action;
    }

    private void CancelConfirmation() => _pendingAction = null;

    private async Task ConfirmActionAsync()
    {
        if (_pendingAction is { } action)
        {
            await ExecuteAsync(action);
        }
    }

    private async Task ExecuteAsync(DocumentManagerEditorialReviewAction action)
    {
        if (_selectedDraft is null || _isSaving)
        {
            return;
        }

        _isSaving = true;
        _message = null;

        try
        {
            await using var scope = ServiceScopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<
                ReviewDocumentManagerEditorialDraftHandler>();
            var updated = await handler.HandleAsync(
                _form.ToCommand(_selectedDraft, action),
                CancellationToken.None);

            _selectedDraft = updated;
            _form = EditorialForm.FromDraft(updated);
            _pendingAction = null;
            await RefreshSummariesAsync(scope.ServiceProvider);
            ShowSuccess(action switch
            {
                DocumentManagerEditorialReviewAction.Save =>
                    Text("La fiche a été enregistrée.", "The record was saved."),
                DocumentManagerEditorialReviewAction.Approve =>
                    Text("La fiche a été approuvée.", "The record was approved."),
                _ => Text("La fiche a été rejetée.", "The record was rejected.")
            });
        }
        catch (DocumentManagerEditorialDraftConcurrencyException)
        {
            _pendingAction = null;
            ShowError(Text(
                "Cette fiche a été modifiée ailleurs. Sa version la plus récente va être rechargée.",
                "This record was changed elsewhere. Its latest version will be reloaded."));
            await ReloadSelectedAsync();
        }
        catch (DocumentManagerEditorialReviewValidationException exception)
        {
            ShowError(LocalizeValidation(exception.Message));
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task ReloadSelectedAsync()
    {
        if (_selectedDraft is null)
        {
            return;
        }

        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        await LoadDraftAsync(scope.ServiceProvider, _selectedDraft.Id);
        await RefreshSummariesAsync(scope.ServiceProvider);
    }

    private async Task RefreshSummariesAsync(IServiceProvider services)
    {
        var handler = services.GetRequiredService<
            ListDocumentManagerEditorialDraftsHandler>();
        _drafts = await handler.HandleAsync(CancellationToken.None);
    }

    private bool MatchesFilter(DocumentManagerEditorialDraftSummary draft) =>
        _statusFilter switch
        {
            "pending" => draft.Status == DocumentManagerEditorialDraftStatus.PendingReview,
            "in-review" => draft.Status == DocumentManagerEditorialDraftStatus.InReview,
            "approved" => draft.Status == DocumentManagerEditorialDraftStatus.Approved,
            "rejected" => draft.Status == DocumentManagerEditorialDraftStatus.Rejected,
            _ => true
        };

    private string StatusLabel(DocumentManagerEditorialDraftStatus status) =>
        status switch
        {
            DocumentManagerEditorialDraftStatus.PendingReview => Text("À vérifier", "Pending"),
            DocumentManagerEditorialDraftStatus.InReview => Text("En cours", "In review"),
            DocumentManagerEditorialDraftStatus.Approved => Text("Approuvée", "Approved"),
            DocumentManagerEditorialDraftStatus.Rejected => Text("Rejetée", "Rejected"),
            _ => status.ToString()
        };

    private static string StatusCss(DocumentManagerEditorialDraftStatus status) =>
        status switch
        {
            DocumentManagerEditorialDraftStatus.PendingReview => "pending",
            DocumentManagerEditorialDraftStatus.InReview => "in-review",
            DocumentManagerEditorialDraftStatus.Approved => "approved",
            DocumentManagerEditorialDraftStatus.Rejected => "rejected",
            _ => string.Empty
        };

    private string PartCountLabel(int count) =>
        Language == ApplicationLanguage.French
            ? $"{count} partie{(count > 1 ? "s" : string.Empty)}"
            : $"{count} part{(count == 1 ? string.Empty : "s")}";

    private string ScopeLabel(DocumentManagerResultScope scope)
    {
        if (scope.StartPhysicalPageNumber is { } start &&
            scope.EndPhysicalPageNumber is { } end)
        {
            return start == end
                ? Text($"page {start}", $"page {start}")
                : Text($"pages {start}–{end}", $"pages {start}–{end}");
        }

        return scope.Title ?? Text("Document entier", "Whole document");
    }

    private string FormatDate(DateTimeOffset value) =>
        value.ToLocalTime().ToString(Language == ApplicationLanguage.French
            ? "dd/MM/yyyy HH:mm"
            : "MM/dd/yyyy h:mm tt");

    private string ConfirmationTitle(DocumentManagerEditorialReviewAction action) =>
        action == DocumentManagerEditorialReviewAction.Approve
            ? Text("Approuver cette fiche ?", "Approve this record?")
            : Text("Rejeter cette fiche ?", "Reject this record?");

    private string ConfirmationText(DocumentManagerEditorialReviewAction action) =>
        action == DocumentManagerEditorialReviewAction.Approve
            ? Text(
                "Elle sera prête pour la création de l’ouvrage dans la bibliothèque.",
                "It will be ready for creation of the library work.")
            : Text(
                "Le motif du rejet sera conservé dans l’historique.",
                "The rejection reason will be kept in the audit history.");

    private string ConfirmationButton(DocumentManagerEditorialReviewAction action) =>
        action == DocumentManagerEditorialReviewAction.Approve
            ? Text("Confirmer l’approbation", "Confirm approval")
            : Text("Confirmer le rejet", "Confirm rejection");

    private string LocalizeValidation(string message)
    {
        if (Language != ApplicationLanguage.French)
        {
            return message;
        }

        return message switch
        {
            "Title is required." => "Le titre est obligatoire.",
            "The primary contributor name and role must be provided together." =>
                "Le nom du contributeur principal et son rôle doivent être renseignés ensemble.",
            "Approval requires a title, language, and primary contributor." =>
                "L’approbation exige un titre, une langue et un contributeur principal.",
            "Rejection requires a reason." => "Le motif du rejet est obligatoire.",
            "Publication year must be between 1 and 9999." =>
                "L’année de publication doit être comprise entre 1 et 9999.",
            _ => message
        };
    }

    private string Text(string french, string english) =>
        Language == ApplicationLanguage.French ? french : english;

    private void ShowSuccess(string message)
    {
        _message = message;
        _messageIsError = false;
    }

    private void ShowError(string message)
    {
        _message = message;
        _messageIsError = true;
    }

    private sealed class EditorialForm
    {
        public string Title { get; set; } = string.Empty;
        public string? PrimaryContributorName { get; set; }
        public string? PrimaryContributorRole { get; set; }
        public string? LanguageCode { get; set; }
        public string? EditionStatement { get; set; }
        public int? PublicationYear { get; set; }
        public string? PublicationPlace { get; set; }
        public string? Description { get; set; }
        public string? RejectionReason { get; set; }

        public static EditorialForm FromDraft(DocumentManagerEditorialDraft draft) =>
            new()
            {
                Title = draft.Title,
                PrimaryContributorName = draft.PrimaryContributorName,
                PrimaryContributorRole = draft.PrimaryContributorRole,
                LanguageCode = draft.LanguageCode,
                EditionStatement = draft.EditionStatement,
                PublicationYear = draft.PublicationYear,
                PublicationPlace = draft.PublicationPlace,
                Description = draft.Description,
                RejectionReason = draft.RejectionReason
            };

        public DocumentManagerEditorialDraftReviewCommand ToCommand(
            DocumentManagerEditorialDraft draft,
            DocumentManagerEditorialReviewAction action) =>
            new(
                draft.Id,
                draft.Version,
                action,
                Title,
                PrimaryContributorName,
                PrimaryContributorRole,
                LanguageCode,
                EditionStatement,
                PublicationYear,
                PublicationPlace,
                Description,
                RejectionReason);
    }
}
