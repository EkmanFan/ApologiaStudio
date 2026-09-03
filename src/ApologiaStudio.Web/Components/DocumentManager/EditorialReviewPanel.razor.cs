using ApologiaStudio.Application.Abstractions.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.Web;
using ApologiaStudio.Application.Knowledge.MetadataReview;
using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Application.Knowledge.DocumentProcessing;
using ApologiaStudio.Domain.Users;
using ApologiaStudio.Web.DocumentManager;
using Microsoft.AspNetCore.Components;

namespace ApologiaStudio.Web.Components.DocumentManager;

public partial class EditorialReviewPanel
{
    [Inject]
    private IServiceScopeFactory ServiceScopeFactory { get; set; } = null!;

    [Inject]
    private IDocumentManagerAdministrationAuthorizer AdministrationAuthorizer
    {
        get;
        set;
    } = null!;

    [Inject]
    private DocumentManagerConsumerOptions ConsumerOptions { get; set; } = null!;

    private IReadOnlyList<GenreFormTermView> _genreFormTerms = [];

    private IReadOnlyList<GenreFormSuggestion>? _suggestions;

    private bool _isAnalyzing;

    private string? _analysisError;

    private Guid? _currentAnalysisId;

    private IReadOnlyList<string> _suggestedAtAnalysis = [];

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
    private EditorialConfirmationAction? _pendingAction;
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

    private bool CanAdminister => AdministrationAuthorizer.IsAuthorized;

    private bool CanReplayDelivery =>
        CanAdminister && ConsumerOptions.CanRequestReplay;

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
        _suggestions = null;
        _analysisError = null;
        _currentAnalysisId = null;
        _suggestedAtAnalysis = [];
        await LoadGenreFormVocabularyAsync();
    }

    private Task SaveAsync() => ExecuteAsync(DocumentManagerEditorialReviewAction.Save);

    private void HandleContributorRoleChanged(ChangeEventArgs eventArgs)
    {
        _form.PrimaryContributorRole = eventArgs.Value?.ToString();
    }

    private void AskConfirmation(EditorialConfirmationAction action)
    {
        _message = null;
        _pendingAction = action;
    }

    private void CancelConfirmation() => _pendingAction = null;

    private async Task ConfirmActionAsync()
    {
        if (_pendingAction is { } action)
        {
            switch (action)
            {
                case EditorialConfirmationAction.Approve:
                    await ExecuteAsync(
                        DocumentManagerEditorialReviewAction.Approve);
                    break;
                case EditorialConfirmationAction.Reject:
                    await ExecuteAsync(
                        DocumentManagerEditorialReviewAction.Reject);
                    break;
                case EditorialConfirmationAction.Reopen:
                    await ReopenAsync();
                    break;
                case EditorialConfirmationAction.Purge:
                    await PurgeAsync(replayAfterPurge: false);
                    break;
                case EditorialConfirmationAction.PurgeAndReplay:
                    await PurgeAsync(replayAfterPurge: true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(action),
                        action,
                        null);
            }
        }
    }

    private async Task ReopenAsync()
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
                ReopenDocumentManagerEditorialDraftHandler>();
            var reopened = await handler.HandleAsync(
                new ReopenDocumentManagerEditorialDraftCommand(
                    _selectedDraft.Id,
                    _selectedDraft.Version),
                CancellationToken.None);

            _selectedDraft = reopened;
            _form = EditorialForm.FromDraft(reopened);
            _pendingAction = null;
            await RefreshSummariesAsync(scope.ServiceProvider);
            ShowSuccess(Text(
                "La fiche a été rouverte pour révision.",
                "The record was reopened for review."));
        }
        catch (Exception exception)
        {
            HandleAdministrativeException(exception);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task PurgeAsync(bool replayAfterPurge)
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
                PurgeDocumentManagerSubmissionHandler>();
            var purged = await handler.HandleAsync(
                new PurgeDocumentManagerSubmissionCommand(
                    _selectedDraft.Id,
                    _selectedDraft.Version),
                CancellationToken.None);

            _selectedDraft = null;
            _pendingAction = null;

            if (replayAfterPurge)
            {
                try
                {
                    var replayClient = scope.ServiceProvider
                        .GetRequiredService<IDocumentManagerDeliveryReplayClient>();
                    await replayClient.ReplaySubmissionAsync(
                        purged.SubmissionId,
                        CancellationToken.None);
                }
                catch (Exception exception)
                {
                    await LoadAsync();
                    ShowError(Text(
                        $"La copie Apologia a été supprimée, mais le Manager n’a pas pu programmer la nouvelle livraison : {exception.Message}",
                        $"The Apologia copy was deleted, but Manager could not schedule redelivery: {exception.Message}"));
                    return;
                }
            }

            await LoadAsync();
            ShowSuccess(replayAfterPurge
                ? Text(
                    "La copie Apologia a été supprimée et la nouvelle livraison a été demandée au Manager.",
                    "The Apologia copy was deleted and a new delivery was requested from Manager.")
                : Text(
                    "L’ouvrage a été supprimé définitivement d’Apologia.",
                    "The work was permanently deleted from Apologia."));
        }
        catch (Exception exception)
        {
            HandleAdministrativeException(exception);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void HandleAdministrativeException(Exception exception)
    {
        _pendingAction = null;

        if (exception is DocumentManagerEditorialDraftConcurrencyException)
        {
            ShowError(Text(
                "Cette fiche a été modifiée ailleurs. Rechargez-la avant de recommencer.",
                "This record changed elsewhere. Reload it before trying again."));
            return;
        }

        if (exception is DocumentManagerAdministrationForbiddenException)
        {
            ShowError(Text(
                "Cette action est réservée à l’administration.",
                "This action is restricted to administration."));
            return;
        }

        ShowError(exception.Message);
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

            // The editorial save has committed; recording what the reviewer
            // did with the proposal is evaluation data and cannot undo it.
            await RecordReviewerOutcomeAsync(
                updated.GenreForms.Select(x => x.AuthorityUri).ToList());

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

    private string ConfirmationTitle(EditorialConfirmationAction action) =>
        action switch
        {
            EditorialConfirmationAction.Approve =>
                Text("Approuver cette fiche ?", "Approve this record?"),
            EditorialConfirmationAction.Reject =>
                Text("Rejeter cette fiche ?", "Reject this record?"),
            EditorialConfirmationAction.Reopen =>
                Text("Rouvrir cette fiche ?", "Reopen this record?"),
            EditorialConfirmationAction.Purge =>
                Text(
                    "Supprimer définitivement cet ouvrage d’Apologia ?",
                    "Permanently delete this work from Apologia?"),
            EditorialConfirmationAction.PurgeAndReplay =>
                Text(
                    "Supprimer puis réimporter cet ouvrage ?",
                    "Delete and reimport this work?"),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };

    private string ConfirmationText(EditorialConfirmationAction action) =>
        action switch
        {
            EditorialConfirmationAction.Approve => Text(
                "Elle sera prête pour la création de l’ouvrage dans la bibliothèque.",
                "It will be ready for creation of the library work."),
            EditorialConfirmationAction.Reject => Text(
                "Le motif du rejet sera conservé dans l’historique.",
                "The rejection reason will be kept in the audit history."),
            EditorialConfirmationAction.Reopen => Text(
                "Le rejet restera dans l’historique, mais la fiche redeviendra modifiable.",
                "The rejection will remain in history, but the record will become editable again."),
            EditorialConfirmationAction.Purge => Text(
                "Cette action irréversible supprimera la fiche, ses parties, son historique, les résultats bruts, les visuels et le manifeste conservés par Apologia. Les données du Manager ne seront pas supprimées.",
                "This irreversible action deletes the record, its parts, history, raw results, visuals, and manifest stored by Apologia. Manager data will not be deleted."),
            EditorialConfirmationAction.PurgeAndReplay => Text(
                "Apologia supprimera sa copie complète, puis demandera au Manager de remettre toutes les parties à disposition. DPEngine ne retraitera pas le document.",
                "Apologia will delete its complete copy, then ask Manager to make every part available again. DPEngine will not process the document again."),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };

    private string ConfirmationButton(EditorialConfirmationAction action) =>
        action switch
        {
            EditorialConfirmationAction.Approve =>
                Text("Confirmer l’approbation", "Confirm approval"),
            EditorialConfirmationAction.Reject =>
                Text("Confirmer le rejet", "Confirm rejection"),
            EditorialConfirmationAction.Reopen =>
                Text("Confirmer la réouverture", "Confirm reopening"),
            EditorialConfirmationAction.Purge =>
                Text("Supprimer définitivement", "Delete permanently"),
            EditorialConfirmationAction.PurgeAndReplay =>
                Text("Supprimer et réimporter", "Delete and reimport"),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };

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

    private bool IsGenreFormSelected(string authorityUri) =>
        _form.GenreFormAuthorityUris.Contains(authorityUri, StringComparer.Ordinal);

    private void ToggleGenreForm(string authorityUri, ChangeEventArgs args)
    {
        var selected = args.Value is true;

        if (selected)
        {
            if (!IsGenreFormSelected(authorityUri))
            {
                _form.GenreFormAuthorityUris.Add(authorityUri);
            }

            return;
        }

        _form.GenreFormAuthorityUris.RemoveAll(
            x => string.Equals(x, authorityUri, StringComparison.Ordinal));
    }

    /// <summary>
    /// The closed vocabulary comes from the active profile; the panel never
    /// restates the terms or the hierarchy rules.
    /// </summary>
    private async Task LoadGenreFormVocabularyAsync()
    {
        try
        {
            await using var scope = ServiceScopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider
                .GetRequiredService<IGenreFormAuthorityStore>();

            _genreFormTerms = await store.GetSelectableTermsAsync(
                CancellationToken.None);
        }
        catch (Exception)
        {
            // A missing vocabulary must not prevent editorial review.
            _genreFormTerms = [];
        }
    }

    /// <summary>
    /// Runs the assistant. A suggestion is never written: it only populates a
    /// panel the reviewer may accept, adjust or ignore.
    /// </summary>
    private async Task RunGenreFormAnalysisAsync()
    {
        if (_selectedDraft is null || _isAnalyzing)
        {
            return;
        }

        _isAnalyzing = true;
        _analysisError = null;
        _suggestions = null;

        var requestedAt = DateTimeOffset.UtcNow;

        try
        {
            await using var scope = ServiceScopeFactory.CreateAsyncScope();
            var classifier = scope.ServiceProvider
                .GetRequiredService<IGenreFormClassifier>();

            var validation = await classifier.ClassifyAsync(
                BuildEvidence(),
                CancellationToken.None);

            if (validation.IsValid)
            {
                _suggestions = validation.Result!.Suggested;
                _suggestedAtAnalysis = _suggestions
                    .Select(x => x.AuthorityUri)
                    .ToList();

                await RecordAnalysisAsync(validation.Result, requestedAt);
            }
            else
            {
                // Invalid model output is discarded whole; it is recorded as a
                // failed run and never becomes a persisted suggestion.
                _analysisError = Text(
                    "La proposition de l\u2019assistant a été refusée par la validation.",
                    "The assistant's proposal was refused by validation.");

                await RecordFailureAsync(
                    string.Join(
                        " ",
                        validation.Errors.Select(x => x.Detail)),
                    requestedAt);
            }
        }
        catch (Exception exception)
        {
            _analysisError = Text(
                "L\u2019assistant est indisponible.",
                "The assistant is unavailable.");

            await RecordFailureAsync(exception.GetType().Name, requestedAt);
        }
        finally
        {
            _isAnalyzing = false;
        }
    }

    /// <summary>
    /// Advisory history is written in its own scope and transaction: failing
    /// to record it must never affect the reviewer's editorial work.
    /// </summary>
    private async Task RecordAnalysisAsync(
        GenreFormClassificationResult result,
        DateTimeOffset requestedAt)
    {
        if (_selectedDraft is null)
        {
            return;
        }

        try
        {
            var completedAt = DateTimeOffset.UtcNow;

            await using var scope = ServiceScopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider
                .GetRequiredService<IMetadataReviewAnalysisStore>();
            var actor = scope.ServiceProvider
                .GetRequiredService<ICurrentUser>().UserId.Value;

            var analysis = await store.RecordAsync(
                new RecordMetadataReviewAnalysisCommand(
                    _selectedDraft.Id,
                    actor,
                    result,
                    requestedAt,
                    completedAt,
                    (completedAt - requestedAt).TotalMilliseconds),
                CancellationToken.None);

            _currentAnalysisId = analysis.Id;
        }
        catch (Exception)
        {
            // Evaluation history is best effort; review continues regardless.
            _currentAnalysisId = null;
        }
    }

    private async Task RecordFailureAsync(
        string reason,
        DateTimeOffset requestedAt)
    {
        if (_selectedDraft is null)
        {
            return;
        }

        try
        {
            var completedAt = DateTimeOffset.UtcNow;

            await using var scope = ServiceScopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider
                .GetRequiredService<IMetadataReviewAnalysisStore>();
            var actor = scope.ServiceProvider
                .GetRequiredService<ICurrentUser>().UserId.Value;

            await store.RecordFailureAsync(
                new RecordFailedMetadataReviewAnalysisCommand(
                    _selectedDraft.Id,
                    actor,
                    reason,
                    GenreFormProfile.Version,
                    requestedAt,
                    completedAt,
                    (completedAt - requestedAt).TotalMilliseconds),
                CancellationToken.None);
        }
        catch (Exception)
        {
            // Diagnostics must never block the reviewer.
        }

        _currentAnalysisId = null;
        _suggestedAtAnalysis = [];
    }

    /// <summary>
    /// Records what the reviewer decided, after their editorial save has
    /// already committed.
    /// </summary>
    private async Task RecordReviewerOutcomeAsync(
        IReadOnlyList<string> confirmed)
    {
        if (_currentAnalysisId is null)
        {
            return;
        }

        try
        {
            var outcome = MetadataReviewOutcomeCalculator.Determine(
                _suggestedAtAnalysis,
                confirmed);

            await using var scope = ServiceScopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider
                .GetRequiredService<IMetadataReviewAnalysisStore>();
            var reviewer = scope.ServiceProvider
                .GetRequiredService<ICurrentUser>().UserId.Value;

            await store.RecordReviewerOutcomeAsync(
                _currentAnalysisId.Value,
                outcome,
                reviewer,
                DateTimeOffset.UtcNow,
                CancellationToken.None);
        }
        catch (Exception)
        {
            // The editorial save already succeeded; losing the outcome costs
            // evaluation data, never metadata.
        }
    }

    private void AcceptSuggestions()
    {
        if (_suggestions is null)
        {
            return;
        }

        _form.GenreFormAuthorityUris = _suggestions
            .Select(x => x.AuthorityUri)
            .ToList();
    }

    private void RejectSuggestions()
    {
        _suggestions = null;
        _analysisError = null;
    }

    /// <summary>
    /// Bounded evidence taken from the reviewed record itself. Source excerpts
    /// are not duplicated here.
    /// </summary>
    private MetadataReviewEvidence BuildEvidence()
    {
        return new MetadataReviewEvidence(
            _form.Title,
            null,
            string.IsNullOrWhiteSpace(_form.PrimaryContributorName)
                ? []
                : [_form.PrimaryContributorName],
            _form.LanguageCode,
            _form.EditionStatement,
            _form.PublicationYear,
            _form.PublicationPlace,
            _form.Description,
            []);
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

        /// <summary>
        /// Authority URIs chosen by the reviewer. The vocabulary itself is
        /// never restated here: the panel only carries identifiers.
        /// </summary>
        public List<string> GenreFormAuthorityUris { get; set; } = [];

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
                RejectionReason = draft.RejectionReason,
                GenreFormAuthorityUris = draft.GenreForms
                    .Select(x => x.AuthorityUri)
                    .ToList()
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
                GenreFormAuthorityUris,
                RejectionReason);
    }

    private enum EditorialConfirmationAction
    {
        Approve = 0,
        Reject = 1,
        Reopen = 2,
        Purge = 3,
        PurgeAndReplay = 4
    }
}
