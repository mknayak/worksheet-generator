using UglyToad.PdfPig;

namespace WorksheetGenerator.Services;

public class PdfExtractionService
{
    private readonly ILogger<PdfExtractionService> _logger;

    public PdfExtractionService(ILogger<PdfExtractionService> logger)
    {
        _logger = logger;
    }

    public string ExtractText(Stream stream)
    {
        try
        {
            // PdfPig needs the full content, so we buffer it
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Seek(0, SeekOrigin.Begin);

            var sb = new System.Text.StringBuilder();
            using var document = PdfDocument.Open(ms.ToArray());

            foreach (var page in document.GetPages())
            {
                var text = page.Text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine(text);
                    sb.AppendLine();
                }
            }

            var result = sb.ToString().Trim();
            _logger.LogInformation("Extracted {Length} characters from PDF ({Pages} pages)",
                result.Length, document.NumberOfPages);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from PDF stream");
            throw;
        }
    }
}
