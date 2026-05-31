using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using WorksheetGenerator.Models;
using WorksheetGenerator.Services;

namespace WorksheetGenerator.Controllers;

public class WorksheetController : Controller
{
    private readonly IServiceProvider _serviceProvider;
    private readonly PdfExtractionService _pdfExtraction;
    private readonly WorksheetStorageService _worksheetStorage;
    private readonly ProfileStorageService _profileStorage;
    private readonly SessionMaterialStorageService _materialStorage;
    private readonly TemplateStorageService _templateStorage;
    private readonly ILogger<WorksheetController> _logger;

    public WorksheetController(
        IServiceProvider serviceProvider,
        PdfExtractionService pdfExtraction,
        WorksheetStorageService worksheetStorage,
        ProfileStorageService profileStorage,
        SessionMaterialStorageService materialStorage,
        TemplateStorageService templateStorage,
        ILogger<WorksheetController> logger)
    {
        _serviceProvider  = serviceProvider;
        _pdfExtraction    = pdfExtraction;
        _worksheetStorage = worksheetStorage;
        _profileStorage   = profileStorage;
        _materialStorage  = materialStorage;
        _templateStorage  = templateStorage;
        _logger           = logger;
    }

    // POST: /Worksheet/Generate
    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Generate(
        string? profileId, IFormFile? pdfFile, string? manualContent,
        string? materialTitle, string? materialSubject, string? materialClass,
        string? templateId, string? sampleQuestions)
    {
        // Determine input mode: PDF upload or manual text
        bool isManual = !string.IsNullOrWhiteSpace(manualContent);

        if (!isManual && (pdfFile == null || pdfFile.Length == 0))
        {
            TempData["Error"] = "Please upload a PDF or enter content manually.";
            return RedirectToAction("Index", "Home");
        }

        if (!isManual && !pdfFile!.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Only PDF files are supported.";
            return RedirectToAction("Index", "Home");
        }

        var aiService = _serviceProvider.GetRequiredKeyedService<IAiWorksheetService>("openai");

        // Load template if selected
        WorksheetTemplate? template = null;
        if (!string.IsNullOrEmpty(templateId))
            template = await _templateStorage.GetAsync(templateId);

        try
        {
            string sourceText;
            string sourceFileName;

            if (isManual)
            {
                sourceText     = manualContent!.Trim();
                sourceFileName = "manual-input";

                if (sourceText.Length < 20)
                {
                    TempData["Error"] = "Please enter more content — at least a few sentences are needed to generate questions.";
                    return RedirectToAction("Index", "Home");
                }
            }
            else
            {
                sourceFileName = pdfFile!.FileName;
                using (var stream = pdfFile.OpenReadStream())
                    sourceText = _pdfExtraction.ExtractText(stream);

                if (string.IsNullOrWhiteSpace(sourceText) || sourceText.Length < 50)
                {
                    TempData["Error"] = "Could not extract readable text from the PDF. Make sure it is a text-based PDF (not a scanned image).";
                    return RedirectToAction("Index", "Home");
                }
            }

            // Save as a library material
            var material = new SessionMaterial
            {
                Title            = string.IsNullOrWhiteSpace(materialTitle)
                                       ? (isManual ? "Manual Input" : Path.GetFileNameWithoutExtension(pdfFile!.FileName))
                                       : materialTitle,
                Subject          = materialSubject  ?? string.Empty,
                ClassName        = materialClass    ?? string.Empty,
                OriginalFileName = sourceFileName,
                ExtractedText    = sourceText
            };
            await _materialStorage.SaveAsync(material);

            // Generate worksheet via AI
            var samples   = string.IsNullOrWhiteSpace(sampleQuestions) ? null : sampleQuestions.Trim();
            var worksheet = await aiService.GenerateWorksheetAsync(sourceText, sourceFileName, template, samples);
            worksheet.StudentProfileId  = profileId ?? string.Empty;
            worksheet.SessionMaterialId = material.Id;

            await _worksheetStorage.SaveAsync(worksheet);

            return RedirectToAction(nameof(View), new { id = worksheet.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Worksheet generation failed");
            TempData["Error"] = $"Generation failed: {ex.Message}";
            return RedirectToAction("Index", "Home");
        }
    }

    // GET: /Worksheet/View/{id}
    public async Task<IActionResult> View(string id)
    {
        var worksheet = await _worksheetStorage.GetAsync(id);
        if (worksheet == null) return NotFound();

        var profile = string.IsNullOrEmpty(worksheet.StudentProfileId)
            ? null
            : await _profileStorage.GetAsync(worksheet.StudentProfileId);

        return View(new WorksheetViewModel { Worksheet = worksheet, Student = profile });
    }

    // GET: /Worksheet/PrintWorksheet/{id}
    public async Task<IActionResult> PrintWorksheet(string id)
    {
        var worksheet = await _worksheetStorage.GetAsync(id);
        if (worksheet == null) return NotFound();

        var profile = string.IsNullOrEmpty(worksheet.StudentProfileId)
            ? null
            : await _profileStorage.GetAsync(worksheet.StudentProfileId);

        return View(new WorksheetViewModel { Worksheet = worksheet, Student = profile });
    }

    // GET: /Worksheet/PrintAnswers/{id}
    public async Task<IActionResult> PrintAnswers(string id)
    {
        var worksheet = await _worksheetStorage.GetAsync(id);
        if (worksheet == null) return NotFound();

        var profile = string.IsNullOrEmpty(worksheet.StudentProfileId)
            ? null
            : await _profileStorage.GetAsync(worksheet.StudentProfileId);

        return View(new WorksheetViewModel { Worksheet = worksheet, Student = profile });
    }

    // GET: /Worksheet/DownloadJson/{id}
    public async Task<IActionResult> DownloadJson(string id)
    {
        var worksheet = await _worksheetStorage.GetAsync(id);
        if (worksheet == null) return NotFound();

        var json = System.Text.Json.JsonSerializer.Serialize(worksheet, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        var bytes    = System.Text.Encoding.UTF8.GetBytes(json);
        var safeName = worksheet.Title.Replace(" ", "_").Replace("/", "-");
        var fileName = $"worksheet_{safeName}_{id[..8]}.json";

        return File(bytes, "application/json", fileName);
    }

    // GET: /Worksheet/History
    public async Task<IActionResult> History()
    {
        var worksheets  = await _worksheetStorage.GetAllAsync();
        var profiles    = await _profileStorage.GetAllAsync();
        var profileDict = profiles.ToDictionary(p => p.Id, p => p.Name);

        var summaries = worksheets.Select(w => new WorksheetSummary
        {
            Id               = w.Id,
            Title            = w.Title,
            Subject          = w.Subject,
            Topic            = w.Topic,
            GeneratedAt      = w.GeneratedAt,
            StudentProfileId = w.StudentProfileId,
            StudentName      = profileDict.TryGetValue(w.StudentProfileId, out var name) ? name : "—",
            QuestionCount    = w.Questions.Count,
            TotalPoints      = w.TotalPoints
        }).ToList();

        return View(new WorksheetHistoryViewModel { Worksheets = summaries });
    }

    // POST: /Worksheet/Delete/{id}
    [HttpPost]
    public IActionResult Delete(string id)
    {
        _worksheetStorage.Delete(id);
        TempData["Success"] = "Worksheet deleted.";
        return RedirectToAction(nameof(History));
    }
}
