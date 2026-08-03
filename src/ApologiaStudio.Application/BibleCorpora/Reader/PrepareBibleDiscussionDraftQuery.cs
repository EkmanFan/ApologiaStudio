using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.Application.BibleCorpora.Reader;

public sealed record PrepareBibleDiscussionDraftQuery(
    string EditionCode,
    string BookCode,
    int ChapterNumber,
    string StartVerseLabel,
    string? EndVerseLabel,
    ApplicationLanguage Language);

public sealed record BibleDiscussionDraft(
    string Prompt,
    string NormalizedReference);
