using System.Globalization;

namespace ApologiaStudio.KnowledgeImporter;

internal static class KnowledgeGroundedAnswerCli
{
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        var options = GroundedAnswerOptions.Parse(args);
        var connectionString = Environment.GetEnvironmentVariable(
            "APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new KnowledgeImportException(
                "APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION must be defined for answer-grounded.");
        }

        using var embeddingClient = new OllamaEmbeddingClient(
            new Uri(DeDecretisRetrievalProfile.OllamaBaseAddress));
        var embeddingDigest = await embeddingClient.ResolveModelDigestAsync(
            DeDecretisRetrievalProfile.EmbeddingModel,
            cancellationToken);
        var instructedQuery = DeDecretisVectorSearchProfile.FormatQuery(options.Query);
        var embeddings = await embeddingClient.EmbedAsync(
            DeDecretisRetrievalProfile.EmbeddingModel,
            DeDecretisRetrievalProfile.EmbeddingDimensions,
            [instructedQuery],
            cancellationToken);
        var embeddingDigestAfter = await embeddingClient.ResolveModelDigestAsync(
            DeDecretisRetrievalProfile.EmbeddingModel,
            cancellationToken);
        if (!string.Equals(
                embeddingDigest,
                embeddingDigestAfter,
                StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                "The Ollama embedding model changed while the grounded-answer query was being embedded.");
        }

        var search = await KnowledgeVectorSearch.SearchAsync(
            connectionString,
            embeddings[0],
            embeddingDigest,
            DeDecretisGroundedAnswerProfile.CandidateChunkK,
            options.Mode,
            cancellationToken);
        var evidence = BuildEvidence(search.Results);

        using var generationClient = new OllamaGroundedGenerationClient(
            new Uri(DeDecretisRetrievalProfile.OllamaBaseAddress),
            TimeSpan.FromSeconds(
                DeDecretisGroundedAnswerProfile.GenerationTimeoutSeconds));
        var generationDigest = await generationClient.ResolveModelDigestAsync(
            DeDecretisGroundedAnswerProfile.GenerationModel,
            cancellationToken);
        var generated = await generationClient.GenerateAsync(
            DeDecretisGroundedAnswerProfile.GenerationModel,
            options.Query,
            evidence,
            cancellationToken);
        var generationDigestAfter = await generationClient.ResolveModelDigestAsync(
            DeDecretisGroundedAnswerProfile.GenerationModel,
            cancellationToken);
        if (!string.Equals(
                generationDigest,
                generationDigestAfter,
                StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                "The Ollama generation model changed while the grounded answer was being generated.");
        }

        var validated = ValidateModelResponse(
            generated.Response,
            evidence);
        WriteResult(
            options,
            search,
            evidence,
            embeddingDigest,
            generationDigest,
            generated,
            validated);

        return 0;
    }

    private static IReadOnlyList<GroundedEvidence> BuildEvidence(
        IReadOnlyList<KnowledgeVectorSearchResult> results)
    {
        var evidence = new List<GroundedEvidence>(
            DeDecretisGroundedAnswerProfile.EvidenceSegmentK);
        var seenSegments = new HashSet<Guid>();

        foreach (var result in results)
        {
            if (!seenSegments.Add(result.SegmentId))
            {
                continue;
            }

            var locator = result.SegmentLocator ??
                          result.SegmentTitle ??
                          $"§{result.SegmentOrdinal}";
            var citationLabel = string.IsNullOrWhiteSpace(result.CitationLabel)
                ? result.WorkTitle
                : result.CitationLabel!;
            evidence.Add(
                new GroundedEvidence(
                    $"E{evidence.Count + 1}",
                    result.SegmentId,
                    result.SegmentOrdinal,
                    locator,
                    result.WorkTitle,
                    citationLabel,
                    result.SegmentText,
                    result.Similarity));

            if (evidence.Count == DeDecretisGroundedAnswerProfile.EvidenceSegmentK)
            {
                break;
            }
        }

        if (evidence.Count == 0)
        {
            throw new KnowledgeImportException(
                "Grounded generation could not construct any citable evidence segment from retrieval results.");
        }

        return evidence;
    }

    private static ValidatedGroundedAnswer ValidateModelResponse(
        GroundedAnswerModelResponse response,
        IReadOnlyList<GroundedEvidence> evidence)
    {
        var status = response.Status?.Trim();
        if (status is not ("answered" or "insufficient_evidence"))
        {
            throw new KnowledgeImportException(
                "Grounded generation returned an invalid status.");
        }

        if (response.Claims is null)
        {
            throw new KnowledgeImportException(
                "Grounded generation omitted the claims array.");
        }

        if (status == "insufficient_evidence")
        {
            if (response.Claims.Length != 0)
            {
                throw new KnowledgeImportException(
                    "An insufficient-evidence response must not contain factual claims.");
            }

            return new ValidatedGroundedAnswer(
                IsInsufficientEvidence: true,
                Claims: []);
        }

        if (response.Claims.Length is < 1 or > DeDecretisGroundedAnswerProfile.MaximumClaims)
        {
            throw new KnowledgeImportException(
                $"An answered grounded response must contain between 1 and {DeDecretisGroundedAnswerProfile.MaximumClaims} claims.");
        }

        var evidenceById = evidence.ToDictionary(
            item => item.EvidenceId,
            StringComparer.Ordinal);
        var validatedClaims = new List<ValidatedGroundedClaim>(
            response.Claims.Length);

        foreach (var claim in response.Claims)
        {
            var text = claim.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text) ||
                text.Length > DeDecretisGroundedAnswerProfile.MaximumClaimCharacters)
            {
                throw new KnowledgeImportException(
                    "Grounded generation returned an empty or oversized claim.");
            }

            if (claim.EvidenceIds is not { Length: >= 1 } ||
                claim.EvidenceIds.Length > DeDecretisGroundedAnswerProfile.MaximumEvidenceIdsPerClaim)
            {
                throw new KnowledgeImportException(
                    "Every grounded claim must cite a bounded non-empty evidence-id list.");
            }

            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            var resolvedEvidence = new List<GroundedEvidence>(
                claim.EvidenceIds.Length);
            foreach (var evidenceId in claim.EvidenceIds)
            {
                if (string.IsNullOrWhiteSpace(evidenceId) ||
                    !uniqueIds.Add(evidenceId) ||
                    !evidenceById.TryGetValue(evidenceId, out var resolved))
                {
                    throw new KnowledgeImportException(
                        $"Grounded generation cited unknown or duplicate evidence id '{evidenceId}'.");
                }

                resolvedEvidence.Add(resolved);
            }

            validatedClaims.Add(
                new ValidatedGroundedClaim(
                    text,
                    resolvedEvidence));
        }

        return new ValidatedGroundedAnswer(
            IsInsufficientEvidence: false,
            Claims: validatedClaims);
    }

    private static void WriteResult(
        GroundedAnswerOptions options,
        KnowledgeVectorSearchResponse search,
        IReadOnlyList<GroundedEvidence> evidence,
        string embeddingDigest,
        string generationDigest,
        OllamaGroundedGenerationResult generation,
        ValidatedGroundedAnswer answer)
    {
        Console.WriteLine($"Grounded answer profile: {DeDecretisGroundedAnswerProfile.ProfileId}");
        Console.WriteLine($"Search profile: {DeDecretisVectorSearchProfile.ProfileId}");
        Console.WriteLine($"Retrieval profile: {DeDecretisRetrievalProfile.ProfileId}");
        Console.WriteLine($"Query: {options.Query}");
        Console.WriteLine($"Mode: {options.Mode.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Embedding model: {DeDecretisRetrievalProfile.EmbeddingModel}");
        Console.WriteLine($"Embedding digest: {embeddingDigest}");
        Console.WriteLine($"Generation model: {DeDecretisGroundedAnswerProfile.GenerationModel}");
        Console.WriteLine($"Generation digest: {generationDigest}");
        Console.WriteLine($"Evidence segments: {evidence.Count}");
        Console.WriteLine($"HNSW index verified: {(search.HnswIndexVerified ? "yes" : "not requested")}");
        Console.WriteLine($"Generation prompt tokens: {generation.PromptEvaluationCount?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}");
        Console.WriteLine($"Generation output tokens: {generation.EvaluationCount?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}");
        Console.WriteLine($"Generation total ms: {FormatNanoseconds(generation.TotalDurationNanoseconds)}");
        Console.WriteLine($"Generation load ms: {FormatNanoseconds(generation.LoadDurationNanoseconds)}");
        Console.WriteLine("Application citation validation: PASS");

        if (answer.IsInsufficientEvidence)
        {
            Console.WriteLine("RESULT: INSUFFICIENT_EVIDENCE");
            Console.WriteLine("Claims: 0");
            Console.WriteLine("Citations: 0");
            return;
        }

        var citationNumbers = new Dictionary<string, int>(StringComparer.Ordinal);
        var orderedCitations = new List<GroundedEvidence>();
        foreach (var claim in answer.Claims)
        {
            foreach (var source in claim.Evidence)
            {
                if (citationNumbers.ContainsKey(source.EvidenceId))
                {
                    continue;
                }

                citationNumbers[source.EvidenceId] = orderedCitations.Count + 1;
                orderedCitations.Add(source);
            }
        }

        Console.WriteLine("RESULT: ANSWERED");
        Console.WriteLine($"Claims: {answer.Claims.Count}");
        Console.WriteLine($"Citations: {orderedCitations.Count}");
        Console.WriteLine();
        Console.WriteLine("Answer:");
        foreach (var claim in answer.Claims)
        {
            var markers = string.Concat(
                claim.Evidence.Select(
                    source => $"[{citationNumbers[source.EvidenceId]}]"));
            Console.WriteLine($"- {claim.Text} {markers}");
        }

        Console.WriteLine();
        Console.WriteLine("Citations:");
        for (var index = 0; index < orderedCitations.Count; index++)
        {
            var source = orderedCitations[index];
            Console.WriteLine(
                $"[{index + 1}] {source.CitationLabel} — {source.Locator} " +
                $"(segment {source.SegmentOrdinal}, similarity={source.Similarity.ToString("F6", CultureInfo.InvariantCulture)})");
        }

        Console.WriteLine(
            "Cited segments: " +
            string.Join(
                ", ",
                orderedCitations.Select(source => $"§{source.SegmentOrdinal}")));
    }

    private static string FormatNanoseconds(long? nanoseconds)
    {
        if (nanoseconds is null)
        {
            return "unknown";
        }

        return (nanoseconds.Value / 1_000_000d)
            .ToString("F1", CultureInfo.InvariantCulture);
    }

    private sealed record GroundedAnswerOptions(
        string Query,
        VectorSearchMode Mode)
    {
        public static GroundedAnswerOptions Parse(IReadOnlyList<string> args)
        {
            string? query = null;
            var mode = VectorSearchMode.Exact;

            for (var index = 0; index < args.Count; index++)
            {
                switch (args[index])
                {
                    case "--query":
                        query = ReadValue(args, ref index, "--query");
                        break;
                    case "--mode":
                        mode = ReadValue(args, ref index, "--mode") switch
                        {
                            "exact" => VectorSearchMode.Exact,
                            "hnsw" => VectorSearchMode.Hnsw,
                            var value => throw new KnowledgeImportException(
                                $"Invalid --mode value '{value}'. Expected exact or hnsw.")
                        };
                        break;
                    default:
                        throw new KnowledgeImportException(
                            $"Unknown answer-grounded option: {args[index]}");
                }
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                throw new KnowledgeImportException(
                    "Missing required option --query for answer-grounded.");
            }

            return new GroundedAnswerOptions(
                query.Trim(),
                mode);
        }

        private static string ReadValue(
            IReadOnlyList<string> args,
            ref int index,
            string option)
        {
            index++;
            if (index >= args.Count ||
                args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new KnowledgeImportException(
                    $"Missing value for {option}.");
            }

            return args[index];
        }
    }
}
