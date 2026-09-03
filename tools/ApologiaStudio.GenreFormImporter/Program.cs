using ApologiaStudio.GenreFormImporter;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

return await GenreFormImportCli.RunAsync(args, cancellation.Token);
