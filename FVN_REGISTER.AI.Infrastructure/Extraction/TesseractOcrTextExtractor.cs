using System.Diagnostics;
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Configuration;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.AI.Infrastructure.Extraction;

/// <summary>
/// OCR adapter for image files and scanned PDFs. Uses local Tesseract and Poppler executables;
/// processes are launched without a shell and receive only generated temporary file paths.
/// </summary>
public sealed class TesseractOcrTextExtractor(IOptions<OcrOptions> options) : IOcrTextExtractor
{
    private readonly OcrOptions settings = options.Value;
    private const long MaximumInputBytes = 25L * 1024 * 1024;
    private const int MaximumPdfPages = 100;
    private const int MaximumOutputCharacters = 2_000_000;

    public async Task<string> ExtractTextAsync(Stream content, string mediaType,
        CancellationToken cancellationToken = default)
    {
        if (!content.CanRead) throw new ArgumentException("OCR input stream is not readable.", nameof(content));
        var originalPosition = content.CanSeek ? content.Position : 0;
        var directory = Path.Combine(Path.GetTempPath(), "FvnAiOcr", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var extension = mediaType.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "application/pdf" => ".pdf",
                _ => throw new NotSupportedException($"OCR does not support media type '{mediaType}'.")
            };
            var input = Path.Combine(directory, "source" + extension);
            await CopyBoundedAsync(content, input, cancellationToken);
            var images = new List<string>();
            if (extension == ".pdf")
            {
                if (string.IsNullOrWhiteSpace(settings.PdfToPpmExecutable))
                    throw new NotSupportedException("PDF rasterizer is not configured.");
                var prefix = Path.Combine(directory, "page");
                await RunAsync(settings.PdfToPpmExecutable,
                    ["-f", "1", "-l", MaximumPdfPages.ToString(), "-r", Math.Clamp(settings.Dpi, 100, 300).ToString(),
                     "-png", input, prefix], directory, cancellationToken);
                images.AddRange(Directory.GetFiles(directory, "page-*.png").OrderBy(x => x, StringComparer.Ordinal));
                if (images.Count == 0) throw new InvalidDataException("PDF_RASTERIZATION_PRODUCED_NO_PAGES");
                if (images.Count > MaximumPdfPages) throw new InvalidDataException("PDF_OCR_PAGE_LIMIT_EXCEEDED");
            }
            else images.Add(input);

            var output = new System.Text.StringBuilder();
            for (var index = 0; index < images.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pageText = await RunAsync(settings.TesseractExecutable,
                    [images[index], "stdout", "-l", SafeLanguages(settings.Languages), "--psm", "6"],
                    directory, cancellationToken);
                output.AppendLine($"[PAGE {index + 1}]");
                output.AppendLine(pageText);
                if (output.Length > MaximumOutputCharacters)
                    throw new InvalidDataException("OCR_OUTPUT_EXCEEDS_LIMIT");
            }
            if (string.IsNullOrWhiteSpace(output.ToString()))
                throw new InvalidDataException("OCR_RETURNED_EMPTY_TEXT");
            return output.ToString();
        }
        finally
        {
            if (content.CanSeek) content.Position = originalPosition;
            try { Directory.Delete(directory, recursive: true); } catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private async Task CopyBoundedAsync(Stream source, string destination, CancellationToken cancellationToken)
    {
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            total += read;
            if (total > MaximumInputBytes) throw new InvalidDataException("OCR_INPUT_EXCEEDS_LIMIT");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        if (total == 0) throw new InvalidDataException("OCR_INPUT_EMPTY");
    }

    private async Task<string> RunAsync(string executable, IReadOnlyList<string> arguments,
        string workingDirectory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(executable))
            throw new NotSupportedException("OCR executable is not configured.");
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 10, 600)));
        if (!process.Start()) throw new InvalidOperationException("OCR_PROCESS_START_FAILED");
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            if (cancellationToken.IsCancellationRequested) throw;
            throw new TimeoutException("OCR_PROCESS_TIMEOUT");
        }
        var output = await stdout;
        var error = await stderr;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"OCR_PROCESS_FAILED_{process.ExitCode}: {error[..Math.Min(error.Length, 500)]}");
        return output;
    }

    private static string SafeLanguages(string value)
    {
        var languages = value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (languages.Length is < 1 or > 5 || languages.Any(x => x.Length > 12 || x.Any(c => !char.IsAsciiLetterOrDigit(c))))
            throw new InvalidOperationException("OCR_LANGUAGES_INVALID");
        return string.Join('+', languages);
    }
}
