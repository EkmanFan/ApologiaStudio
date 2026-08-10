using ApologiaStudio.KnowledgeImporter;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

return await KnowledgeImportCli.RunAsync(args, cancellation.Token);
