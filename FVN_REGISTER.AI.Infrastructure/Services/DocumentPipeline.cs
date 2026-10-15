using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>Fail-closed document pipeline. Only positively scanned documents can become review candidates.</summary>
public sealed class DocumentPipeline(
    AiPlatformDbContext db,
    IOriginalDocumentStore store,
    IMalwareScanner scanner,
    IDocumentTextExtractor textExtractor,
    IDocumentClassifier classifier,
    IExtractionSchemaRegistry schemaRegistry,
    ISchemaDrivenDocumentExtractor structuredExtractor,
    IExtractionValidationService validationService) : IDocumentPipeline
{
    private const string ParserVersion = "local-text-v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task ProcessAsync(Guid documentId, string workerId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var leaseOwned = await db.IngestionJobs.AnyAsync(
            x => x.DocumentId == documentId && x.Status == "Running" && x.LeaseOwner == workerId &&
                 x.LeaseUntilUtc.HasValue && x.LeaseUntilUtc.Value > now, cancellationToken);
        if (!leaseOwned) throw new InvalidOperationException("INGESTION_LEASE_LOST");

        var document = await db.Documents.SingleOrDefaultAsync(x => x.Id == documentId, cancellationToken)
            ?? throw new InvalidOperationException("INGESTION_DOCUMENT_NOT_FOUND");
        if (document.Status is "Approved" or "Rejected") throw new InvalidOperationException("DOCUMENT_ALREADY_REVIEWED");
        // Idempotency: a worker may crash after persisting the candidate but before completing the queue job.
        if (await db.Extractions.AnyAsync(x => x.DocumentId == documentId, cancellationToken)) return;

        await using var scanStream = await store.OpenReadAsync(document.StorageKey, cancellationToken);
        var scan = await scanner.ScanAsync(scanStream, cancellationToken);
        if (scan.Verdict != MalwareScanVerdict.Clean)
        {
            document.Status = scan.Verdict == MalwareScanVerdict.Infected ? "Rejected" : "Quarantined";
            await db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException(scan.FailureCode ?? (scan.Verdict == MalwareScanVerdict.Infected ? "MALWARE_DETECTED" : "MALWARE_SCAN_UNKNOWN"));
        }

        await using var content = await store.OpenReadAsync(document.StorageKey, cancellationToken);
        var text = await textExtractor.ExtractTextAsync(content, document.MediaType, cancellationToken);
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("EXTRACTION_EMPTY_DOCUMENT");
        if (text.Length > 2_000_000) text = text[..2_000_000];

        var (documentType, confidence) = await classifier.ClassifyAsync(text, cancellationToken);
        object candidate;
        IReadOnlyList<ExtractionValidationIssue> validationIssues = [];
        var parserVersion = ParserVersion;
        var promptVersion = "document-classification-v1";

        if (!string.IsNullOrWhiteSpace(document.ModuleCode) && !string.IsNullOrWhiteSpace(document.SchemaCode))
        {
            var schema = await schemaRegistry.GetPublishedAsync(document.TenantCode, document.ModuleCode,
                document.SchemaCode, cancellationToken)
                ?? throw new InvalidOperationException("EXTRACTION_SCHEMA_NOT_PUBLISHED");
            var extracted = await structuredExtractor.ExtractAsync(text, schema, cancellationToken);
            var validation = await validationService.ValidateAsync(new ExtractionValidationRequest(schema, extracted), cancellationToken);
            validationIssues = validation.Issues;
            parserVersion = "schema-extraction-v1";
            promptVersion = "schema-extraction-v1";
            candidate = new
            {
                schemaCode = schema.SchemaCode,
                schemaVersion = schema.Version,
                documentId = document.Id,
                fileName = document.FileName,
                documentType,
                source = new { sha256 = document.Sha256 },
                extractedFields = extracted.Select(x => new
                {
                    path = x.Path, value = x.Value, confidence = x.Confidence, sourceReference = x.SourceReference, evidenceText = x.EvidenceText
                }).ToArray(),
                validation = new
                {
                    isValid = validation.IsValid,
                    issues = validation.Issues
                },
                warnings = new[] { "AI extraction is a candidate and requires human review.", "No official business data has been changed." }
            };
        }
        else
        {
            candidate = BuildCandidate(document, documentType, text);
        }

        var extraction = new AiExtractionRecord
        {
            Id = Guid.NewGuid(), DocumentId = document.Id, TenantCode = document.TenantCode,
            DocumentType = documentType, ClassificationConfidence = (decimal)confidence,
            CandidateJson = JsonSerializer.Serialize(candidate, JsonOptions),
            ParserVersion = parserVersion, ModelProvider = classifier.GetType().Name,
            ModelVersion = "provider-routed", PromptVersion = promptVersion, CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.Extractions.Add(extraction);
        foreach (var issue in validationIssues)
        {
            db.ValidationIssues.Add(new AiValidationIssueRecord
            {
                Id = Guid.NewGuid(), ExtractionId = extraction.Id, FieldPath = issue.Path,
                RuleCode = issue.Code, Severity = issue.Severity, Message = issue.Message,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        document.Status = "AwaitingHumanReview";
        await db.SaveChangesAsync(cancellationToken);
    }

    private static object BuildCandidate(AiDocumentRecord document, string type, string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var evidenceRows = new List<object>();
        var pageNumber = 1;
        for (var index = 0; index < Math.Min(lines.Length, 500); index++)
        {
            var line = lines[index];
            var pageMarker = System.Text.RegularExpressions.Regex.Match(line, @"^\[PAGE (?<page>\d+)\]$");
            if (pageMarker.Success && int.TryParse(pageMarker.Groups["page"].Value, out var parsedPage))
            {
                pageNumber = parsedPage;
                continue;
            }
            evidenceRows.Add(new { lineNumber = index + 1, pageNumber, text = line.Length > 4000 ? line[..4000] : line });
        }
        var evidence = evidenceRows.ToArray();
        var fields = new Dictionary<string, object?>
        {
            ["dateCandidates"] = System.Text.RegularExpressions.Regex.Matches(text, @"\b(?:\d{4}[-/]\d{1,2}[-/]\d{1,2}|\d{1,2}[-/]\d{1,2}[-/]\d{4})\b").Select(m => m.Value).Distinct().Take(100).ToArray(),
            ["timeCandidates"] = System.Text.RegularExpressions.Regex.Matches(text, @"\b(?:[01]?\d|2[0-3]):[0-5]\d\b").Select(m => m.Value).Distinct().Take(100).ToArray(),
            ["numericCandidates"] = System.Text.RegularExpressions.Regex.Matches(text, @"(?<!\w)-?\d+(?:[.,]\d+)?").Select(m => m.Value).Distinct().Take(200).ToArray(),
            ["hourCandidates"] = System.Text.RegularExpressions.Regex.Matches(text, @"(?i)\b(?<value>\d{1,3}(?:[.,]\d{1,2})?)\s*(?:giờ|hours?|h)\b").Select(m => m.Groups["value"].Value).Distinct().Take(100).ToArray(),
            ["dayCandidates"] = System.Text.RegularExpressions.Regex.Matches(text, @"(?i)\b(?<value>\d{1,3}(?:[.,]\d{1,2})?)\s*(?:ngày|days?)\b").Select(m => m.Groups["value"].Value).Distinct().Take(100).ToArray(),
            ["emailCandidates"] = System.Text.RegularExpressions.Regex.Matches(text, @"(?i)\b[^\s@]+@[^\s@]+\.[^\s@]+\b").Select(m => m.Value).Distinct().Take(100).ToArray()
        };
        return new
        {
            schemaVersion = "1.0", documentId = document.Id, fileName = document.FileName,
            documentType = type, source = new { sha256 = document.Sha256, storageKey = document.StorageKey },
            extractedFields = fields, evidence, fullTextSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant(),
            warnings = new[] { "Candidate is machine-generated and requires human verification.", "No official HR record has been changed." }
        };
    }

    private static string? FindValue(string text, string pattern)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, pattern);
        return match.Success ? match.Value : null;
    }
}
