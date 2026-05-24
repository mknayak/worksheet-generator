using System.ComponentModel.DataAnnotations.Schema;

namespace WorksheetGenerator.Models;

public class WorksheetQuestion
{
    /// <summary>
    /// Question type: multiple_choice | true_false | fill_blank | short_answer
    /// </summary>
    public int Id { get; set; }   // question number from AI (1, 2, 3 …)
    public string Type { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Answer options (multiple_choice / true_false).
    /// Stored as a JSON array inside the Worksheets.Questions column.
    /// </summary>
    public List<string> Options { get; set; } = new();

    public string Answer { get; set; } = string.Empty;
    public int Points { get; set; }
}

public class Worksheet
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public int EstimatedMinutes { get; set; }
    public string StudentProfileId { get; set; } = string.Empty;
    public string SourceFileName { get; set; } = string.Empty;
    public string SessionMaterialId { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Stored as a JSON blob in the Worksheets table via EF value converter.
    /// </summary>
    public List<WorksheetQuestion> Questions { get; set; } = new();

    /// <summary>Computed — not persisted to the database.</summary>
    [NotMapped]
    public int TotalPoints => Questions.Sum(q => q.Points);
}
