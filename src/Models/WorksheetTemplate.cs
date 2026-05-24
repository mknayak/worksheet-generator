namespace WorksheetGenerator.Models;

/// <summary>
/// Defines how many of each question type to include and per-question points.
/// </summary>
public class QuestionTypeConfig
{
    public string Type      { get; set; } = string.Empty;  // multiple_choice | true_false | fill_blank | short_answer | word_problem
    public int    Count     { get; set; } = 1;
    public int    Points    { get; set; } = 5;
}

/// <summary>
/// A reusable worksheet template that controls exactly which question types
/// are generated and any special instructions for the AI.
/// </summary>
public class WorksheetTemplate
{
    public string Id                       { get; set; } = Guid.NewGuid().ToString();
    public string Name                     { get; set; } = string.Empty;
    public string Description              { get; set; } = string.Empty;
    public string SpecialInstructions      { get; set; } = string.Empty;   // passed verbatim to AI prompt
    public DateTime CreatedAt              { get; set; } = DateTime.Now;
    public DateTime UpdatedAt              { get; set; } = DateTime.Now;

    /// <summary>
    /// Overall difficulty target for the worksheet.
    /// Values: "easy" | "medium" | "hard" | "mixed" (default — AI decides the balance).
    /// </summary>
    public string Difficulty { get; set; } = "mixed";

    /// <summary>Stored as a JSON blob — one row per question type the user configures.</summary>
    public List<QuestionTypeConfig> QuestionTypes { get; set; } = new();

    // ── Computed helpers (not stored) ────────────────────────────────────
    public int TotalQuestions => QuestionTypes.Sum(q => q.Count);
    public int TotalPoints    => QuestionTypes.Sum(q => q.Count * q.Points);

    /// <summary>Display label for a difficulty key.</summary>
    public static string DifficultyLabel(string d) => d switch
    {
        "easy"   => "Easy",
        "medium" => "Medium",
        "hard"   => "Hard",
        _        => "Mixed"
    };

    /// <summary>Bootstrap badge colour class for a difficulty key.</summary>
    public static string DifficultyBadgeStyle(string d) => d switch
    {
        "easy"   => "background:#dcfce7;color:#15803d;",   // green
        "medium" => "background:#fef9c3;color:#a16207;",   // yellow
        "hard"   => "background:#fee2e2;color:#b91c1c;",   // red
        _        => "background:#e5e7eb;color:#374151;"    // grey
    };

    /// <summary>Display label for a type key.</summary>
    public static string TypeLabel(string t) => t switch
    {
        "multiple_choice" => "Multiple Choice",
        "true_false"      => "True / False",
        "fill_blank"      => "Fill in the Blank",
        "short_answer"    => "Short Answer",
        "word_problem"    => "Word Problem",
        _                 => t
    };
}
