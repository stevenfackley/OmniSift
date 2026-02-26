// ============================================================
// OmniSift.Api — PDF Text Extractor
// Extracts text from PDF files using PdfPig
// ============================================================

using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace OmniSift.Api.Services;

/// <summary>
/// Extracts text from PDF documents page-by-page using PdfPig.
/// Concatenates pages with page markers for metadata tracking.
/// </summary>
public sealed class PdfTextExtractor : ITextExtractor
{
    private readonly ILogger<PdfTextExtractor> _logger;

    public PdfTextExtractor(ILogger<PdfTextExtractor> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public string SourceType => "pdf";

    /// <inheritdoc />
    public async Task<string> ExtractTextAsync(
        Stream stream,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _logger.LogDebug("Extracting text from PDF: {FileName}", fileName ?? "unknown");

        // PdfPig requires a seekable stream; buffer if needed
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        using var document = PdfDocument.Open(memoryStream);

        var pages = new List<string>();

        foreach (Page page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var text = page.Text;

            if (!string.IsNullOrWhiteSpace(text))
            {
                pages.Add($"[Page {page.Number}]\n{text.Trim()}");
            }
        }

        if (pages.Count == 0)
        {
            _logger.LogWarning("No text extracted from PDF: {FileName}", fileName ?? "unknown");
            return string.Empty;
        }

        _logger.LogInformation(
            "Extracted text from {PageCount} pages of PDF: {FileName}",
            pages.Count, fileName ?? "unknown");

        return string.Join("\n\n", pages);
    }
}
