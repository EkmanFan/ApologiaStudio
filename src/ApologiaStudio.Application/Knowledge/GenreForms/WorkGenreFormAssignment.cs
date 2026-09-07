namespace ApologiaStudio.Application.Knowledge.GenreForms;

/// <summary>
/// Outcome of an authoritative Genre/Form assignment on a Work.
/// </summary>
public sealed record GenreFormAssignmentResult(
    bool Assigned,
    string Reason);

/// <summary>
/// The authoritative Genre/Form assignments of a Work.
/// </summary>
/// <remarks>
/// The identity handled here is the Apologia product term, addressed by its
/// canonical code. No LCGFT URI, authority term identity, external concept
/// identifier or encoder head index appears in this contract: alignment to an
/// external authority is recorded elsewhere and is never what a Work carries.
///
/// Nothing is ever inferred. No assignment is created automatically, and no
/// second term is persisted because another one was assigned.
/// </remarks>
public interface IWorkGenreFormAssignmentStore
{
    /// <summary>
    /// Gets the product terms assigned to a Work, in product display order.
    /// </summary>
    Task<IReadOnlyList<ApologiaGenreFormTerm>> GetWorkGenreFormsAsync(
        Guid workId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Assigns one active canonical product term to a Work. Refuses an unknown
    /// or retired term, an unknown Work, and a duplicate pair.
    /// </summary>
    /// <remarks>
    /// The V1 product taxonomy is flat, so no hierarchy rule applies: two
    /// assignments can never stand in a broader/narrower relation to one
    /// another.
    /// </remarks>
    Task<GenreFormAssignmentResult> AssignAsync(
        Guid workId,
        string productTermCode,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes one product term from a Work.
    /// </summary>
    Task<bool> RemoveAsync(
        Guid workId,
        string productTermCode,
        CancellationToken cancellationToken);
}
