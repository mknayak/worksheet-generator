using WorksheetGenerator.Models;

namespace WorksheetGenerator.Services;

/// <summary>
/// Common contract for AI providers that generate worksheets from PDF text.
/// Registered as keyed services: "claude" and "openai".
/// </summary>
public interface IAiWorksheetService
{
    /// <param name="pdfContent">Extracted plain text from the source PDF.</param>
    /// <param name="sourceFileName">Original file name — used only for metadata.</param>
    /// <param name="template">
    ///   Optional template controlling which question types and how many to generate.
    ///   When null the AI uses its own judgment (12-15 mixed questions).
    /// </param>
    /// <param name="sampleQuestions">
    ///   Optional sample questions provided by the user. The AI will generate
    ///   new questions in a similar style, format, and difficulty.
    /// </param>
    Task<Worksheet> GenerateWorksheetAsync(
        string             pdfContent,
        string             sourceFileName,
        WorksheetTemplate? template        = null,
        string?            sampleQuestions = null);
}
