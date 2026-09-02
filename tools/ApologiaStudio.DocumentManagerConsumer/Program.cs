using ApologiaStudio.Application.Knowledge.DocumentProcessing;
using ApologiaStudio.DocumentManagerConsumer;
using ApologiaStudio.Infrastructure.Knowledge.DocumentProcessing;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;

if (args.Contains("--help", StringComparer.Ordinal))
{
    WriteUsage();
    return 0;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    var settings = DocumentManagerConsumerSettings.Parse(args);
    var contextOptions =
        new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(
                settings.KnowledgeConnectionString,
                options => options.UseVector())
            .Options;

    await using (var migrationContext =
                 new KnowledgeDbContext(contextOptions))
    {
        await migrationContext.Database.MigrateAsync(
            cancellation.Token);
    }

    using var httpClient =
        new HttpClient
        {
            BaseAddress = settings.Manager.BaseAddress,
            Timeout = settings.RequestTimeout
        };
    var source =
        new HttpDocumentManagerResultSource(
            httpClient,
            settings.Manager);

    Console.WriteLine(
        $"Document Manager consumer '{settings.Manager.ConsumerId}' is connected to {settings.Manager.BaseAddress}.");

    do
    {
        try
        {
            var result =
                await ConsumeOnceAsync(
                    source,
                    contextOptions,
                    cancellation.Token);

            WriteResult(result);

            if (!settings.RunContinuously)
            {
                return 0;
            }

            if (result.Status ==
                DocumentManagerConsumeStatus.NoResultAvailable)
            {
                await Task.Delay(
                    settings.PollInterval,
                    cancellation.Token);
            }
        }
        catch (Exception exception) when (
            settings.RunContinuously &&
            exception is HttpRequestException or NpgsqlException)
        {
            Console.Error.WriteLine(
                $"Transient consumer failure: {exception.Message}");
            await Task.Delay(
                settings.PollInterval,
                cancellation.Token);
        }
    }
    while (!cancellation.IsCancellationRequested);

    return 0;
}
catch (OperationCanceledException)
{
    return 130;
}
catch (Exception exception) when (
    exception is ArgumentException
        or InvalidOperationException
        or HttpRequestException
        or NpgsqlException
        or DocumentManagerResultIntegrityException)
{
    Console.Error.WriteLine(
        $"Document Manager consumer failed: {exception.Message}");
    return 1;
}

static async Task<DocumentManagerConsumeResult> ConsumeOnceAsync(
    IDocumentManagerResultSource source,
    DbContextOptions<KnowledgeDbContext> contextOptions,
    CancellationToken cancellationToken)
{
    await using var context =
        new KnowledgeDbContext(contextOptions);
    var inbox =
        new PostgreSqlDocumentManagerResultInbox(context);
    var assemblyReader =
        new PostgreSqlDocumentManagerSubmissionAssemblyReader(context);
    var draftStore =
        new PostgreSqlDocumentManagerEditorialDraftStore(context);
    var draftPreparer =
        new PrepareDocumentManagerEditorialDraftHandler(
            assemblyReader,
            draftStore,
            TimeProvider.System);
    var handler =
        new ConsumeDocumentManagerResultHandler(
            source,
            inbox,
            draftPreparer,
            TimeProvider.System);

    return await handler.HandleAsync(cancellationToken);
}

static void WriteResult(DocumentManagerConsumeResult result)
{
    switch (result.Status)
    {
        case DocumentManagerConsumeStatus.NoResultAvailable:
            Console.WriteLine("No result is currently available.");
            break;
        case DocumentManagerConsumeStatus.StoredAndAcknowledged:
            Console.WriteLine(
                $"Stored and acknowledged result '{result.ResultReference}'.");
            break;
        case DocumentManagerConsumeStatus.AlreadyStoredAndAcknowledged:
            Console.WriteLine(
                $"Verified existing result and acknowledged replay '{result.ResultReference}'.");
            break;
        default:
            throw new ArgumentOutOfRangeException(
                nameof(result.Status),
                result.Status,
                "Unknown consumer result status.");
    }

    if (result.DraftPreparation is null)
    {
        return;
    }

    var preparation = result.DraftPreparation;
    var assembly = preparation.Assembly;

    switch (preparation.Status)
    {
        case DocumentManagerEditorialDraftPreparationStatus.AwaitingParts:
            Console.WriteLine(
                $"Work '{assembly.OriginalFileName}' is waiting for parts " +
                $"({assembly.ReceivedPartCount}/{assembly.ExpectedPartCount} received).");
            break;
        case DocumentManagerEditorialDraftPreparationStatus.Created:
            Console.WriteLine(
                $"Created provisional record '{preparation.Draft!.Title}' " +
                $"from {assembly.ExpectedPartCount} complete part(s); editorial review is pending.");
            break;
        case DocumentManagerEditorialDraftPreparationStatus.AlreadyExists:
            Console.WriteLine(
                $"Provisional record '{preparation.Draft!.Title}' already exists; editorial changes were preserved.");
            break;
        case DocumentManagerEditorialDraftPreparationStatus.Blocked:
            Console.WriteLine(
                $"Work '{assembly.OriginalFileName}' cannot be assembled: " +
                string.Join(
                    " ",
                    assembly.Issues.Select(issue => issue.Message)));
            break;
        default:
            throw new ArgumentOutOfRangeException(
                nameof(preparation.Status),
                preparation.Status,
                "Unknown editorial draft preparation status.");
    }
}

static void WriteUsage()
{
    Console.WriteLine(
        "Usage: ApologiaStudio.DocumentManagerConsumer <consume-once|run>");
    Console.WriteLine();
    Console.WriteLine("Required environment:");
    Console.WriteLine("  APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION");
    Console.WriteLine("  APOLOGIASTUDIO_DOCUMENT_MANAGER_CONSUMER_KEY");
    Console.WriteLine();
    Console.WriteLine("Optional environment:");
    Console.WriteLine("  APOLOGIASTUDIO_DOCUMENT_MANAGER_URL");
    Console.WriteLine("  APOLOGIASTUDIO_DOCUMENT_MANAGER_CONSUMER_ID");
    Console.WriteLine("  APOLOGIASTUDIO_DOCUMENT_MANAGER_POLL_SECONDS");
    Console.WriteLine("  APOLOGIASTUDIO_DOCUMENT_MANAGER_TIMEOUT_SECONDS");
}
