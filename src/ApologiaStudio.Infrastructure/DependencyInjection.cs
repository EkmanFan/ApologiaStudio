using ApologiaStudio.Application.Abstractions.AiRuntime;
using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Application.Abstractions.BibleCorpora;
using ApologiaStudio.Application.Abstractions.Persistence;
using ApologiaStudio.Application.Abstractions.Preferences;
using ApologiaStudio.Application.Abstractions.Navigation;
using ApologiaStudio.Application.Abstractions.Projects;
using ApologiaStudio.Application.BibleCorpora.Ingestion;
using ApologiaStudio.Infrastructure.BibleCorpora.Ingestion;
using ApologiaStudio.Infrastructure.Persistence;
using ApologiaStudio.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        services.TryAddSingleton(
            TimeProvider.System);

        services.AddScoped<
            IBibleCorpusImporter,
            PostgreSqlBibleCorpusImporter>();

        services.AddScoped<
            IAiRuntimeSettingsStore,
            EfAiRuntimeSettingsStore>();

        return services;
    }
}
