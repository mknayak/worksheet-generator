using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using WorksheetGenerator.Models;
using WorksheetGenerator.Services;

namespace WorksheetGenerator.Controllers;

public class MaterialController : Controller
{
    private readonly SessionMaterialStorageService _materialStorage;
    private readonly WorksheetStorageService _worksheetStorage;
    private readonly ProfileStorageService _profileStorage;
    private readonly TemplateStorageService _templateStorage;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MaterialController> _logger;

    public MaterialController(
        SessionMaterialStorageService materialStorage,
        WorksheetStorageService worksheetStorage,
        ProfileStorageService profileStorage,
        TemplateStorageService templateStorage,
        IServiceProvider serviceProvider,
        ILogger<MaterialController> logger)
    {
        _materialStorage  = materialStorage;
        _worksheetStorage = worksheetStorage;
        _profileStorage   = profileStorage;
        _templateStorage  = templateStorage;
        _serviceProvider  = serviceProvider;
        _logger           = logger;
    }

    // GET: /Material
    public async Task<IActionResult> Index(string? subject, string? className)
    {
        var all = await _materialStorage.GetAllAsync();

        var subjects = all.Select(m => m.Subject).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(s => s).ToList();
        var classes  = all.Select(m => m.ClassName).Where(c => !string.IsNullOrEmpty(c)).Distinct().OrderBy(c => c).ToList();

        var filtered = all
            .Where(m => string.IsNullOrEmpty(subject)   || m.Subject.Equals(subject, StringComparison.OrdinalIgnoreCase))
            .Where(m => string.IsNullOrEmpty(className) || m.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return View(new MaterialLibraryViewModel
        {
            Materials     = filtered,
            Subjects      = subjects,
            Classes       = classes,
            FilterSubject = subject,
            FilterClass   = className
        });
    }

    // GET: /Material/Detail/{id}
    public async Task<IActionResult> Detail(string id)
    {
        var material = await _materialStorage.GetAsync(id);
        if (material == null) return NotFound();

        var allWorksheets = await _worksheetStorage.GetAllAsync();
        var profiles      = await _profileStorage.GetAllAsync();
        var templates     = await _templateStorage.GetAllAsync();
        var profileDict   = profiles.ToDictionary(p => p.Id, p => p.Name);

        var linked = allWorksheets
            .Where(w => w.SessionMaterialId == id)
            .Select(w => new WorksheetSummary
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
            })
            .OrderByDescending(w => w.GeneratedAt)
            .ToList();

        return View(new MaterialDetailViewModel
        {
            Material   = material,
            Worksheets = linked,
            Profiles   = profiles,
            Templates  = templates
        });
    }

    // POST: /Material/GenerateWorksheet
    [HttpPost]
    public async Task<IActionResult> GenerateWorksheet(
        string materialId, string? profileId, string? templateId, string? sampleQuestions)
    {
        var material = await _materialStorage.GetAsync(materialId);
        if (material == null)
        {
            TempData["Error"] = "Material not found.";
            return RedirectToAction(nameof(Index));
        }

        var aiService = _serviceProvider.GetRequiredKeyedService<IAiWorksheetService>("openai");

        // Load template if selected
        WorksheetTemplate? template = null;
        if (!string.IsNullOrEmpty(templateId))
            template = await _templateStorage.GetAsync(templateId);

        // Normalize sample questions (null if empty/whitespace)
        var samples = string.IsNullOrWhiteSpace(sampleQuestions) ? null : sampleQuestions.Trim();

        try
        {
            var worksheet = await aiService.GenerateWorksheetAsync(
                material.ExtractedText, material.OriginalFileName, template, samples);
            worksheet.StudentProfileId  = profileId ?? string.Empty;
            worksheet.SessionMaterialId = materialId;

            await _worksheetStorage.SaveAsync(worksheet);

            return RedirectToAction("View", "Worksheet", new { id = worksheet.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate worksheet from material {MaterialId}", materialId);
            TempData["Error"] = $"Generation failed: {ex.Message}";
            return RedirectToAction(nameof(Detail), new { id = materialId });
        }
    }

    // GET: /Material/Edit/{id}
    public async Task<IActionResult> Edit(string id)
    {
        var material = await _materialStorage.GetAsync(id);
        if (material == null) return NotFound();
        return View(material);
    }

    // POST: /Material/Edit/{id}
    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Edit(
        string id,
        string title, string? subject, string? className,
        string contentMode,          // "keep" | "pdf" | "manual" | "image"
        IFormFile? pdfFile,
        string? manualContent,
        IFormFile? imageFile)
    {
        var material = await _materialStorage.GetAsync(id);
        if (material == null) return NotFound();

        // ── Update metadata ──────────────────────────────────────────────────
        material.Title     = string.IsNullOrWhiteSpace(title) ? material.Title : title.Trim();
        material.Subject   = subject?.Trim()   ?? string.Empty;
        material.ClassName = className?.Trim()  ?? string.Empty;

        // ── Update content if requested ──────────────────────────────────────
        try
        {
            switch (contentMode)
            {
                case "pdf":
                    if (pdfFile == null || pdfFile.Length == 0)
                    {
                        ModelState.AddModelError("", "Please select a PDF file.");
                        return View(material);
                    }
                    if (!pdfFile.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        ModelState.AddModelError("", "Only PDF files are supported.");
                        return View(material);
                    }
                    var pdfExtraction = HttpContext.RequestServices.GetRequiredService<PdfExtractionService>();
                    using (var stream = pdfFile.OpenReadStream())
                        material.ExtractedText = pdfExtraction.ExtractText(stream);

                    if (string.IsNullOrWhiteSpace(material.ExtractedText) || material.ExtractedText.Length < 50)
                    {
                        ModelState.AddModelError("", "Could not extract text from the PDF. Make sure it's a text-based (not scanned) PDF.");
                        return View(material);
                    }
                    material.OriginalFileName = pdfFile.FileName;
                    break;

                case "manual":
                    if (string.IsNullOrWhiteSpace(manualContent) || manualContent.Trim().Length < 20)
                    {
                        ModelState.AddModelError("", "Please enter at least a few sentences of content.");
                        return View(material);
                    }
                    material.ExtractedText    = manualContent.Trim();
                    material.OriginalFileName = "manual-input";
                    break;

                case "image":
                    if (imageFile == null || imageFile.Length == 0)
                    {
                        ModelState.AddModelError("", "Please select an image file.");
                        return View(material);
                    }
                    // Store a note — actual text will be read by vision model at generation time
                    material.ExtractedText    = "[Content provided as image — GPT-4o Vision will read it at generation time]";
                    material.OriginalFileName = imageFile.FileName;

                    // Persist image bytes in a sidecar file named by material ID
                    var uploadsDir = Path.Combine(
                        HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath,
                        "uploads");
                    Directory.CreateDirectory(uploadsDir);
                    var ext       = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                    var imagePath = Path.Combine(uploadsDir, $"{id}{ext}");
                    await using (var fs = System.IO.File.Create(imagePath))
                        await imageFile.CopyToAsync(fs);
                    break;

                // "keep": no content change
            }

            await _materialStorage.SaveAsync(material);
            TempData["Success"] = "Material updated successfully.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update material {Id}", id);
            ModelState.AddModelError("", $"Update failed: {ex.Message}");
            return View(material);
        }
    }

    // POST: /Material/Delete/{id}
    [HttpPost]
    public IActionResult Delete(string id)
    {
        _materialStorage.Delete(id);
        TempData["Success"] = "Material deleted from library.";
        return RedirectToAction(nameof(Index));
    }
}
