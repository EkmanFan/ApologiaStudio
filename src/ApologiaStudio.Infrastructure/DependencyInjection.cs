using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Application.Abstractions.AiRuntime;
using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Application.Abstractions.BibleCorpora;
using ApologiaStudio.Application.Abstractions.Persistence;
using ApologiaStudio.Application.Abstractions.Preferences;
using ApologiaStudio.Application.Abstractions.Navigation;
using ApologiaStudio.Application.Abstractions.Projects;
using ApologiaStudio.Application.BibleCorpora.Ingestion;
using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Application.Knowledge.MetadataReview;
using ApologiaStudio.Infrastructure.Knowledge.MetadataReview;
using ApologiaStudio.Application.Knowledge.Ingestion;
using ApologiaStudio.Application.Knowledge.DocumentProcessing;
using ApologiaStudio.Infrastructure.BibleCorpora.Ingestion;
using ApologiaStudio.Infrastructure.Knowledge.DocumentProcessing;
using ApologiaStudio.Infrastructure.Knowledge.GenreForms;
using ApologiaStudio.Infrastructure.Knowledge.Ingestion;
using ApologiaStudio.Infrastructure.Persistence;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using ApologiaStudio.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pgvector.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString(
                "ApologiaStudio");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'ApologiaStudio' was not configured.");
        }

        services.AddDbContext<ApologiaStudioDbContext>(
            options =>
                options.UseNpgsql(connectionString));

        var knowledgeConnectionString =
            configuration.GetConnectionString("Knowledge") ??
            Environment.GetEnvironmentVariable(
                "APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION");

        if (string.IsNullOrWhiteSpace(knowledgeConnectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'Knowledge' or environment variable " +
                "APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION was not configured.");
        }

        services.AddDbContext<KnowledgeDbContext>(
            options =>
                options.UseNpgsql(
                    knowledgeConnectionString,
                    postgres => postgres.UseVector()));

        services.AddScoped<
            IDocumentManagerEditorialReviewStore,
            PostgreSqlDocumentManagerEditorialReviewStore>();
        services.AddScoped<
            IDocumentManagerEditorialAdministrationStore,
            PostgreSqlDocumentManagerEditorialAdministrationStore>();
        services.AddScoped<
            IDocumentManagerResultInbox,
            PostgreSqlDocumentManagerResultInbox>();
        services.AddScoped<
            IGenreFormAuthorityStore,
            PostgreSqlGenreFormAuthorityStore>();
        services.AddSingleton<
            IGenreFormAuthorityDatasetReader,
            SkosJsonLdGenreFormDatasetReader>();
        services.AddScoped<
            IGenreFormProfileSeeder,
            PostgreSqlGenreFormProfileSeeder>();
        services.AddScoped<
            IGenreFormAssignmentStore,
            PostgreSqlGenreFormAssignmentStore>();
        services.AddScoped<
            IGenreFormPolicyProvider,
            KnowledgeStoreGenreFormPolicyProvider>();
        services.AddSingleton<
            IGenreFormClassificationValidator,
            GenreFormClassificationValidator>();
        services.AddScoped<
            IDocumentManagerSubmissionAssemblyReader,
            PostgreSqlDocumentManagerSubmissionAssemblyReader>();
        services.AddScoped<
            IDocumentManagerEditorialDraftStore,
            PostgreSqlDocumentManagerEditorialDraftStore>();
        services.AddScoped<
            IDocumentManagerEditorialDraftPreparer,
            PrepareDocumentManagerEditorialDraftHandler>();

        services.AddScoped<
            IConversationRepository,
            EfConversationRepository>();

        services.AddScoped<
            IBibleCorpusQueryRepository,
            EfBibleCorpusQueryRepository>();

        services.AddScoped<
            IUserPreferencesRepository,
            EfUserPreferencesRepository>();

        services.AddScoped<
            IConversationProjectRepository,
            EfConversationProjectRepository>();

        services.AddScoped<
            ISidebarPinRepository,
            EfSidebarPinRepository>();

        services.AddScoped<
            IUnitOfWork,
            EfUnitOfWork>();

        services.AddSingleton<
            IBibleCorpusReader,
            SilMachineUsfmCorpusReader>();

        services.AddSingleton<
            IPdfDocumentExtractor,
            PdfPigDocumentExtractor>();

        services.AddSingleton<
            IPdfDocumentNormalizer,
            PdfDocumentNormalizer>();

        services.AddSingleton<
            IDocumentSegmenter,
            HeuristicDocumentSegmenter>();

        services.TryAddSingleton(
            TimeProvider.System);

        services.AddScoped<
            IBibleCorpusImporter,
            PostgreSqlBibleCorpusImporter>();

        services.AddScoped<
            IAiRuntimeSettingsStore,
            EfAiRuntimeSettingsStore>();

        services.AddScoped<
            IAgentSettingsStore,
            EfAgentSettingsStore>();

        return services;
    }
}
