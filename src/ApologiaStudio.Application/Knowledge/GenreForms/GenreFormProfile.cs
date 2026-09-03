namespace ApologiaStudio.Application.Knowledge.GenreForms;

/// <summary>
/// Apologia Genre/Form Profile V1.
///
/// Product-owned reference data: the approved terms are declared by their
/// authority preferred label and resolved against the imported LCGFT snapshot
/// at seed time. Identifiers are never hard-coded, so the profile cannot drift
/// from the authority it claims to follow.
/// </summary>
public static class GenreFormProfile
{
    public const string Version = "apologia-genre-form-profile-v1";

    /// <summary>
    /// The fourteen terms approved for editorial assignment. The order is the
    /// approved specification order and becomes the profile display order.
    /// </summary>
    public static IReadOnlyList<string> SelectableLabels { get; } =
    [
        "Apologetic writings",
        "Textbooks",
        "Sacred works",
        "Pastoral letters and charges",
        "Sermons",
        "Catechisms",
        "Creeds",
        "Devotional literature",
        "Hagiographies",
        "Prayers",
        "Biographies",
        "Academic theses",
        "Essays",
        "Commentaries"
    ];
}

public sealed record GenreFormProfileSeedResult(
    string ProfileVersion,
    int SelectableCount,
    int StructuralOnlyCount,
    IReadOnlyList<string> StructuralOnlyLabels,
    bool Changed);

public interface IGenreFormProfileSeeder
{
    /// <summary>
    /// Applies the profile over the imported authority. Deterministic and
    /// idempotent; never mutates authority facts or existing Work assignments.
    /// </summary>
    Task<GenreFormProfileSeedResult> ApplyAsync(
        CancellationToken cancellationToken);
}

public sealed record GenreFormAssignmentResult(
    bool Assigned,
    string Reason);

public interface IGenreFormAssignmentStore
{
    Task<IReadOnlyList<GenreFormTermView>> GetWorkGenreFormsAsync(
        Guid workId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Assigns one selectable term to a Work. Refuses a non-selectable term, a
    /// duplicate pair, and a term that is an ancestor of one already assigned.
    /// Never persists broader terms implicitly.
    /// </summary>
    Task<GenreFormAssignmentResult> AssignAsync(
        Guid workId,
        string authorityUri,
        CancellationToken cancellationToken);

    Task<bool> RemoveAsync(
        Guid workId,
        string authorityUri,
        CancellationToken cancellationToken);
}
