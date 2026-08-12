namespace ApologiaStudio.Application.Knowledge.Ingestion;

public sealed record KnowledgeRetrievalChunk(
    Guid Id,
    int Ordinal,
    Guid SegmentId,
    int SegmentOrdinal,
    int StartOffset,
    int EndOffset,
    string Text);
