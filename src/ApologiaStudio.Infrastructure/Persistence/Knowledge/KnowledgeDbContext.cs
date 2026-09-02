using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure.Persistence.Knowledge;

public sealed class KnowledgeDbContext(
    DbContextOptions<KnowledgeDbContext> options)
    : DbContext(options)
{
    internal DbSet<KnowledgeResourceEntity> Resources =>
        Set<KnowledgeResourceEntity>();

    internal DbSet<KnowledgeWorkEntity> Works =>
        Set<KnowledgeWorkEntity>();

    internal DbSet<KnowledgeExpressionEntity> Expressions =>
        Set<KnowledgeExpressionEntity>();

    internal DbSet<KnowledgeManifestationEntity> Manifestations =>
        Set<KnowledgeManifestationEntity>();

    internal DbSet<KnowledgeArtifactEntity> Artifacts =>
        Set<KnowledgeArtifactEntity>();

    internal DbSet<KnowledgeDocumentSegmentEntity> DocumentSegments =>
        Set<KnowledgeDocumentSegmentEntity>();

    internal DbSet<KnowledgeRetrievalChunkEntity> RetrievalChunks =>
        Set<KnowledgeRetrievalChunkEntity>();

    internal DbSet<KnowledgeChunkEmbeddingEntity> ChunkEmbeddings =>
        Set<KnowledgeChunkEmbeddingEntity>();

    internal DbSet<DocumentManagerResultInboxEntity> DocumentManagerResults =>
        Set<DocumentManagerResultInboxEntity>();

    internal DbSet<DocumentManagerVisualAssetInboxEntity>
        DocumentManagerVisualAssets =>
            Set<DocumentManagerVisualAssetInboxEntity>();

    internal DbSet<DocumentManagerSubmissionManifestInboxEntity>
        DocumentManagerSubmissionManifests =>
            Set<DocumentManagerSubmissionManifestInboxEntity>();

    internal DbSet<DocumentManagerExpectedUnitInboxEntity>
        DocumentManagerExpectedUnits =>
            Set<DocumentManagerExpectedUnitInboxEntity>();

    internal DbSet<DocumentManagerEditorialDraftEntity>
        DocumentManagerEditorialDrafts =>
            Set<DocumentManagerEditorialDraftEntity>();

    internal DbSet<DocumentManagerEditorialDraftPartEntity>
        DocumentManagerEditorialDraftParts =>
            Set<DocumentManagerEditorialDraftPartEntity>();

    internal DbSet<DocumentManagerEditorialReviewEventEntity>
        DocumentManagerEditorialReviewEvents =>
            Set<DocumentManagerEditorialReviewEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        KnowledgeModelConfiguration.Configure(modelBuilder);
    }
}
