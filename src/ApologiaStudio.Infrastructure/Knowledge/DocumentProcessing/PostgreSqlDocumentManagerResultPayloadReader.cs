using ApologiaStudio.Application.Knowledge.DocumentProcessing;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure.Knowledge.DocumentProcessing;

/// <summary>
/// Reads a stored portable result payload back from the result inbox.
/// </summary>
/// <remarks>
/// The payload is already retained for custody verification; this exposes it
/// for reading rather than storing a second copy of what it contains.
/// </remarks>
public sealed class PostgreSqlDocumentManagerResultPayloadReader(
    KnowledgeDbContext context)
    : IDocumentManagerResultPayloadReader
{
    /// <inheritdoc />
    public async Task<byte[]?> GetFirstAsync(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        // Ordered by the expected unit's ordinal so the chosen part is the
        // document's first, never whichever row the database returns first.
        var payloads =
            from result in context.DocumentManagerResults.AsNoTracking()
            join unit in context.DocumentManagerExpectedUnits.AsNoTracking()
                on result.ProcessingUnitId equals unit.ProcessingUnitId
            where result.SubmissionId == submissionId &&
                  unit.SubmissionId == submissionId
            orderby unit.Ordinal
            select result.Payload;

        return await payloads.FirstOrDefaultAsync(cancellationToken);
    }
}
