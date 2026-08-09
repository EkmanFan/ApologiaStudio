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
using ApologiaStudio.Application.BibleCorpora.Queries;
using ApologiaStudio.Application.BibleCorpora.Reader;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.Web.Components.BibleReader;

public partial class BibleReaderPanel
{
    [Parameter, EditorRequired]
    public BibleReaderView Reader { get; set; } = null!;

    [Parameter]
    public ApplicationLanguage Language { get; set; } =
        ApplicationLanguage.French;

    [Parameter]
    public bool SidebarIsOpen { get; set; }

    [Parameter]
    public bool IsBusy { get; set; }

    [Parameter]
    public string? ErrorMessage { get; set; }

    [Parameter]
    public EventCallback OnOpenSidebar { get; set; }

    [Parameter]
    public EventCallback<BibleReaderSelection> OnUseInDiscussion { get; set; }

    private string? _loadedChapterKey;
    private int? _selectionAnchorOrdinal;
    private int? _selectionEndOrdinal;
    private string? _copyStatus;

    protected override void OnParametersSet()
    {
        var chapter = Reader.Chapter;
        var nextChapterKey = chapter is null
            ? null
            : $"{chapter.Edition.Code}/{chapter.Book.Code}/{chapter.ChapterNumber}";

        if (nextChapterKey == _loadedChapterKey)
        {
            return;
        }

        _loadedChapterKey = nextChapterKey;
        ClearSelection();
    }

    private void ChangeEdition(ChangeEventArgs eventArgs)
    {
        var editionCode = eventArgs.Value?.ToString();

        if (!string.IsNullOrWhiteSpace(editionCode))
        {
            NavigationManager.NavigateTo(
                GetEditionUrl(editionCode));
        }
    }

    private void ChangeBook(ChangeEventArgs eventArgs)
    {
        var bookCode = eventArgs.Value?.ToString();

        if (Reader.Edition is not null &&
            !string.IsNullOrWhiteSpace(bookCode))
        {
            NavigationManager.NavigateTo(
                GetLocationUrl(
                    new BibleReaderLocation(
                        Reader.Edition.Code,
                        bookCode,
                        1)));
        }
    }

    private void ChangeChapter(ChangeEventArgs eventArgs)
    {
        if (Reader.Edition is null ||
            Reader.Chapter is null ||
            !int.TryParse(
                eventArgs.Value?.ToString(),
                out var chapterNumber))
        {
            return;
        }

        NavigationManager.NavigateTo(
            GetLocationUrl(
                new BibleReaderLocation(
                    Reader.Edition.Code,
                    Reader.Chapter.Book.Code,
                    chapterNumber)));
    }

    private void SelectVerse(int verseOrdinal)
    {
        _copyStatus = null;

        if (_selectionAnchorOrdinal is null)
        {
            _selectionAnchorOrdinal = verseOrdinal;
            _selectionEndOrdinal = verseOrdinal;
            return;
        }

        if (_selectionAnchorOrdinal == verseOrdinal &&
            _selectionEndOrdinal == verseOrdinal)
        {
            ClearSelection();
            return;
        }

        _selectionEndOrdinal = verseOrdinal;
    }

    private void ClearSelection()
    {
        _selectionAnchorOrdinal = null;
        _selectionEndOrdinal = null;
        _copyStatus = null;
    }

    private bool IsSelected(int verseOrdinal)
    {
        if (_selectionAnchorOrdinal is null ||
            _selectionEndOrdinal is null)
        {
            return false;
        }

        var minimum = Math.Min(
            _selectionAnchorOrdinal.Value,
            _selectionEndOrdinal.Value);

        var maximum = Math.Max(
            _selectionAnchorOrdinal.Value,
            _selectionEndOrdinal.Value);

        return verseOrdinal >= minimum && verseOrdinal <= maximum;
    }

    private bool TryGetSelection(
        out BibleReaderSelection selection)
    {
        selection = null!;

        if (Reader.Edition is null ||
            Reader.Chapter is null ||
            _selectionAnchorOrdinal is null ||
            _selectionEndOrdinal is null)
        {
            return false;
        }

        var minimum = Math.Min(
            _selectionAnchorOrdinal.Value,
            _selectionEndOrdinal.Value);

        var maximum = Math.Max(
            _selectionAnchorOrdinal.Value,
            _selectionEndOrdinal.Value);

        var startVerse = Reader.Chapter.Verses.SingleOrDefault(
            verse => verse.VerseOrdinal == minimum);

        var endVerse = Reader.Chapter.Verses.SingleOrDefault(
            verse => verse.VerseOrdinal == maximum);

        if (startVerse is null || endVerse is null)
        {
            return false;
        }

        selection = new BibleReaderSelection(
            Reader.Edition.Code,
            Reader.Chapter.Book.Code,
            Reader.Chapter.ChapterNumber,
            startVerse.VerseLabel,
            endVerse.VerseLabel);

        return true;
    }

    private async Task CopyReferenceAsync()
    {
        if (!TryGetSelection(out var selection))
        {
            return;
        }

        var copied = await JsRuntime.InvokeAsync<bool>(
            "apologiaStudio.copyText",
            GetSelectionReference(selection));

        _copyStatus = copied
            ? Text("Référence copiée.", "Reference copied.")
            : Text(
                "La copie automatique a échoué.",
                "Automatic copying failed.");
    }

    private Task UseInDiscussionAsync()
    {
        return TryGetSelection(out var selection)
            ? OnUseInDiscussion.InvokeAsync(selection)
            : Task.CompletedTask;
    }

    private string GetSelectionReference(
        BibleReaderSelection selection)
    {
        var end = selection.StartVerseLabel == selection.EndVerseLabel
            ? string.Empty
            : $"-{selection.EndVerseLabel}";

        return $"{Reader.Chapter!.Book.DisplayName} " +
               $"{selection.ChapterNumber}:" +
               $"{selection.StartVerseLabel}{end} — " +
               Reader.Edition!.DisplayName;
    }

    private string GetVerseAriaLabel(BibleVerseText verse)
    {
        return $"{Text("Verset", "Verse")} {verse.VerseLabel}: {verse.Text}";
    }

    private string GetStateTitle()
    {
        return Reader.Status switch
        {
            BibleReaderStatus.CorpusUnavailable =>
                Text("Corpus indisponible", "Corpus unavailable"),
            BibleReaderStatus.EditionNotFound =>
                Text("Édition inconnue", "Unknown edition"),
            BibleReaderStatus.BookNotFound =>
                Text("Livre introuvable", "Book not found"),
            BibleReaderStatus.ChapterNotFound =>
                Text("Chapitre introuvable", "Chapter not found"),
            _ => Text("Lecture indisponible", "Reader unavailable")
        };
    }

    private string GetStateMessage()
    {
        return Reader.Status switch
        {
            BibleReaderStatus.CorpusUnavailable =>
                Text(
                    "Aucun corpus biblique actif et approuvé ne peut être lu pour le moment.",
                    "No active and approved Bible corpus can be read right now."),
            BibleReaderStatus.EditionNotFound =>
                Text(
                    "Cette édition n’existe pas ou n’est pas disponible.",
                    "This edition does not exist or is unavailable."),
            BibleReaderStatus.BookNotFound =>
                Text(
                    "Ce livre n’appartient pas à l’édition sélectionnée.",
                    "This book does not belong to the selected edition."),
            BibleReaderStatus.ChapterNotFound =>
                Text(
                    "Ce chapitre n’existe pas dans le livre sélectionné.",
                    "This chapter does not exist in the selected book."),
            _ => Text(
                "Le lecteur ne peut pas afficher cette référence.",
                "The reader cannot display this reference.")
        };
    }

    private static string GetEditionUrl(string editionCode)
    {
        return $"/library/{Uri.EscapeDataString(editionCode)}";
    }

    private static string GetLocationUrl(
        BibleReaderLocation location)
    {
        return $"/library/{Uri.EscapeDataString(location.EditionCode)}/" +
               $"{Uri.EscapeDataString(location.BookCode)}/" +
               location.ChapterNumber;
    }

    private string Text(
        string french,
        string english)
    {
        return Language == ApplicationLanguage.French
            ? french
            : english;
    }
}
