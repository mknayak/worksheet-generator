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
        string? profileId, IFormFile? pdfFile,
        string? materialTitle, string? materialSubject, string? materialClass,
        string? templateId)
    {
        if (pdfFile == null || pdfFile.Length == 0)
        {
            TempData["Error"] = "Please select a PDF file to upload.";
            return RedirectToAction("Index", "Home");
        }

        if (!pdfFile.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
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
            // Step 1: Extract text from PDF
            string pdfText;
            using (var stream = pdfFile.OpenReadStream())
                pdfText = _pdfExtraction.ExtractText(stream);

            if (string.IsNullOrWhiteSpace(pdfText) || pdfText.Length < 50)
            {
                TempData["Error"] = "Could not extract readable text from the PDF. Make sure it is a text-based PDF (not a scanned image).";
                return RedirectToAction("Index", "Home");
            }

            // Step 2: Save PDF text as a library material
            var material = new SessionMaterial
            {
                Title            = string.IsNullOrWhiteSpace(materialTitle) ? Path.GetFileNameWithoutExtension(pdfFile.FileName) : materialTitle,
                Subject          = materialSubject ?? string.Empty,
                ClassName        = materialClass   ?? string.Empty,
                OriginalFileName = pdfFile.FileName,
                ExtractedText    = pdfText
            };
            await _materialStorage.SaveAsync(material);

            // Step 3: Generate worksheet via AI (with optional template)
            var worksheet = await aiService.GenerateWorksheetAsync(pdfText, pdfFile.FileName, template);
            worksheet.StudentProfileId  = profileId ?? string.Empty;
            worksheet.SessionMaterialId = material.Id;

            // Step 4: Save worksheet
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
