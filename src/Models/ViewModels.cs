namespace WorksheetGenerator.Models;

public class GenerateWorksheetViewModel
{
    public List<StudentProfile> Profiles { get; set; } = new();
    public string? SelectedProfileId { get; set; }
    public string? ErrorMessage { get; set; }
}

public class WorksheetViewModel
{
    public Worksheet Worksheet { get; set; } = new();
    public StudentProfile? Student { get; set; }
}

public class WorksheetSummary
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public string StudentProfileId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
    public int TotalPoints { get; set; }
}

public class WorksheetHistoryViewModel
{
    public List<WorksheetSummary> Worksheets { get; set; } = new();
}

// ── Material Library ────────────────────────────────────────────────────────

public class MaterialLibraryViewModel
{
    public List<SessionMaterial> Materials { get; set; } = new();

    /// <summary>Distinct subjects across all materials for the filter dropdown.</summary>
    public List<string> Subjects { get; set; } = new();

    /// <summary>Distinct class names across all materials for the filter dropdown.</summary>
    public List<string> Classes { get; set; } = new();

    public string? FilterSubject { get; set; }
    public string? FilterClass { get; set; }
}

public class MaterialDetailViewModel
{
    public SessionMaterial Material { get; set; } = new();
    public List<WorksheetSummary> Worksheets { get; set; } = new();
    public List<StudentProfile> Profiles { get; set; } = new();
    public List<WorksheetTemplate> Templates { get; set; } = new();
}
