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
        string materialId, string? profileId, string? templateId)
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

        try
        {
            var worksheet = await aiService.GenerateWorksheetAsync(
                material.ExtractedText, material.OriginalFileName, template);
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

    // POST: /Material/Delete/{id}
    [HttpPost]
    public IActionResult Delete(string id)
    {
        _materialStorage.Delete(id);
        TempData["Success"] = "Material deleted from library.";
        return RedirectToAction(nameof(Index));
    }
}
