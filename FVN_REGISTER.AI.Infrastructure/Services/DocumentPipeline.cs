using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>Fail-closed document pipeline. Only positively scanned documents can become review candidates.</summary>
public sealed class DocumentPipeline(
    AiPlatformDbContext db,
    IOriginalDocumentStore store,
    IMalwareScanner scanner,
    IDocumentTextExtractor textExtractor) : IDocumentPipeline
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

        var (documentType, confidence) = Classify(document.FileName, document.Extension, text);
        var candidate = BuildCandidate(document, documentType, text);
        var extraction = new AiExtractionRecord
        {
            Id = Guid.NewGuid(), DocumentId = document.Id, TenantCode = document.TenantCode,
            DocumentType = documentType, ClassificationConfidence = (decimal)confidence,
            CandidateJson = JsonSerializer.Serialize(candidate, JsonOptions),
            ParserVersion = ParserVersion, ModelProvider = "DeterministicLocal",
            ModelVersion = ParserVersion, PromptVersion = null, CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.Extractions.Add(extraction);
        document.Status = "AwaitingHumanReview";
        await db.SaveChangesAsync(cancellationToken);
    }

    private static (string Type, double Confidence) Classify(string fileName, string extension, string text)
    {
        var lower = (fileName + "\n" + text[..Math.Min(text.Length, 12000)]).ToLowerInvariant();
        if (lower.Contains("đơn xin nghỉ") || lower.Contains("nghỉ phép") || lower.Contains("leave request"))
            return ("HR.LeaveEvidence", 0.82);
        if (lower.Contains("tăng ca") || lower.Contains("làm thêm giờ") || lower.Contains("overtime"))
            return ("HR.OvertimeEvidence", 0.82);
        if (lower.Contains("đối soát") || lower.Contains("chấm công") || lower.Contains("attendance"))
            return ("HR.AttendanceEvidence", 0.78);
        return (extension.TrimStart('.').ToUpperInvariant() switch { "CSV" => "Tabular.Csv", "XLSX" => "Tabular.Workbook", "TXT" => "Text.Document", _ => "Document.Unknown" }, 0.55);
    }

    private static object BuildCandidate(AiDocumentRecord document, string type, string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var evidence = lines.Take(500).Select((line, index) => new { lineNumber = index + 1, text = line.Length > 4000 ? line[..4000] : line }).ToArray();
        var fields = new Dictionary<string, object?>
        {
            ["employeeCode"] = FindValue(text, @"(?i)\bE\d{4,8}\b"),
            ["dateCandidates"] = System.Text.RegularExpressions.Regex.Matches(text, @"\b(?:\d{4}[-/]\d{1,2}[-/]\d{1,2}|\d{1,2}[-/]\d{1,2}[-/]\d{4})\b").Select(m => m.Value).Distinct().Take(100).ToArray(),
            ["timeCandidates"] = System.Text.RegularExpressions.Regex.Matches(text, @"\b(?:[01]?\d|2[0-3]):[0-5]\d\b").Select(m => m.Value).Distinct().Take(100).ToArray(),
            ["hourCandidates"] = System.Text.RegularExpressions.Regex.Matches(text, @"(?i)\b(?<hours>\d{1,3}(?:[.,]\d{1,2})?)\s*(?:giờ|hours?|h)\b").Select(m => m.Groups["hours"].Value).Distinct().Take(50).ToArray()
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
