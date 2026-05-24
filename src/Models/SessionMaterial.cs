namespace WorksheetGenerator.Models;

/// <summary>
/// Represents a saved PDF session — stores extracted text so worksheets
/// can be re-generated from the same source without re-uploading the PDF.
/// </summary>
public class SessionMaterial
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>User-supplied title, e.g. "Chapter 3 – Photosynthesis"</summary>
    public string Title { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    /// <summary>Class / grade level, e.g. "Grade 10" or "Year 8"</summary>
    public string ClassName { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Full text extracted from the PDF — used by AI services</summary>
    public string ExtractedText { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.Now;
}
