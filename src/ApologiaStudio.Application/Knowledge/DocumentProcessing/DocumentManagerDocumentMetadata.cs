using System.Text.Json;

namespace ApologiaStudio.Application.Knowledge.DocumentProcessing;

/// <summary>
/// Neutral document metadata read from a portable processing result.
/// </summary>
/// <remarks>
/// Only the fields Apologia proposes today are carried. The portable contract
/// exposes more — contributors, publisher, language, dates — and they are
/// deliberately not surfaced yet: adding application fields no reviewer can see
/// would widen the model without widening what it does.
/// </remarks>
public sealed record DocumentManagerDocumentMetadata(
    string? Title,
    string? Description)
{
    /// <summary>
    /// Gets whether the result carried no value Apologia can propose.
    /// </summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Title) &&
        string.IsNullOrWhiteSpace(Description);
}

/// <summary>
/// Reads document metadata from a portable processing-result payload.
/// </summary>
/// <remarks>
/// This reads the portable contract DPEngine publishes; it never opens a PDF or
/// an EPUB. Rediscovering metadata a format adapter already extracted is exactly
/// the duplication the portable result exists to prevent.
/// </remarks>
public static class DocumentManagerPortableMetadataReader
{
    #region Variables and Constants

    /// <summary>
    /// The first portable schema carrying neutral document metadata.
    /// </summary>
    public const string MetadataSchemaVersion =
        "document-processing-result-v5";

    /// <summary>
    /// The schema published before document metadata existed.
    /// </summary>
    public const string LegacySchemaVersion =
        "document-processing-result-v4";

    #endregion

    #region Methods

    /// <summary>
    /// Reads the document metadata a payload carries.
    /// </summary>
    /// <remarks>
    /// A payload from before document metadata existed yields
    /// <see langword="null"/>: absent metadata is a historical fact, not
    /// corruption, and the caller falls back rather than failing.
    ///
    /// A schema this reader does not know fails closed. Decoding an unknown
    /// future version as if it were the current one would let a silently
    /// changed meaning reach a reviewer as fact.
    /// </remarks>
    /// <param name="payload">Portable processing-result payload.</param>
    /// <returns>
    /// Metadata when the payload carries any, otherwise <see langword="null"/>.
    /// </returns>
    public static DocumentManagerDocumentMetadata? Read(
        byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException exception)
        {
            throw new DocumentManagerResultIntegrityException(
                $"The Manager result is not valid JSON: {exception.Message}");
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("schemaVersion", out var schema) ||
                schema.ValueKind != JsonValueKind.String)
            {
                throw new DocumentManagerResultIntegrityException(
                    "The Manager result declares no schema version.");
            }

            var schemaVersion = schema.GetString();

            if (string.Equals(
                    schemaVersion,
                    LegacySchemaVersion,
                    StringComparison.Ordinal))
            {
                return null;
            }

            if (!string.Equals(
                    schemaVersion,
                    MetadataSchemaVersion,
                    StringComparison.Ordinal))
            {
                throw new DocumentManagerResultIntegrityException(
                    $"The Manager result schema '{schemaVersion}' is not supported.");
            }

            if (!root.TryGetProperty("documentMetadata", out var metadata) ||
                metadata.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var read = new DocumentManagerDocumentMetadata(
                ReadValue(metadata, "title"),
                ReadValue(metadata, "description"));

            return read.IsEmpty ? null : read;
        }
    }

    #endregion

    #region Methods Reading

    /// <summary>
    /// Reads one portable metadata value, ignoring its provenance.
    /// </summary>
    /// <remarks>
    /// Origin and source hint are recorded by DPEngine and are not consumed
    /// here: Apologia's own provenance records where the proposal came from,
    /// which is the distinction a reviewer needs.
    /// </remarks>
    private static string? ReadValue(
        JsonElement metadata,
        string propertyName)
    {
        if (!metadata.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Object ||
            !property.TryGetProperty("value", out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    #endregion
}

/// <summary>
/// Reads a stored portable result payload for one submission.
/// </summary>
public interface IDocumentManagerResultPayloadReader
{
    /// <summary>
    /// Returns the payload of the submission's first processing unit.
    /// </summary>
    /// <remarks>
    /// Document metadata describes the whole work, so any part of the same
    /// source carries the same values. The first ordinal is taken so the choice
    /// is deterministic rather than dependent on storage order.
    /// </remarks>
    Task<byte[]?> GetFirstAsync(
        Guid submissionId,
        CancellationToken cancellationToken);
}
