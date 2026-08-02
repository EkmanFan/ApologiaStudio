using ApologiaStudio.BibleCorpusImporter;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

return await ImportCli.RunAsync(args, cancellation.Token);
