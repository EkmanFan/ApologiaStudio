namespace ApologiaStudio.Application.Knowledge.DocumentProcessing;

public sealed class DocumentManagerResultIntegrityException(
    string message)
    : Exception(message);
