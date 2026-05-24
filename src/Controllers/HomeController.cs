using Microsoft.AspNetCore.Mvc;
using WorksheetGenerator.Models;
using WorksheetGenerator.Services;

namespace WorksheetGenerator.Controllers;

public class HomeController : Controller
{
    private readonly ProfileStorageService _profileStorage;
    private readonly WorksheetStorageService _worksheetStorage;
    private readonly SessionMaterialStorageService _materialStorage;
    private readonly TemplateStorageService _templateStorage;

    public HomeController(
        ProfileStorageService profileStorage,
        WorksheetStorageService worksheetStorage,
        SessionMaterialStorageService materialStorage,
        TemplateStorageService templateStorage)
    {
        _profileStorage   = profileStorage;
        _worksheetStorage = worksheetStorage;
        _materialStorage  = materialStorage;
        _templateStorage  = templateStorage;
    }

    public async Task<IActionResult> Index()
    {
        var profiles    = await _profileStorage.GetAllAsync();
        var worksheets  = await _worksheetStorage.GetAllAsync();
        var materials   = await _materialStorage.GetAllAsync();
        var templates   = await _templateStorage.GetAllAsync();
        var profileDict = profiles.ToDictionary(p => p.Id, p => p.Name);

        ViewBag.RecentWorksheets = worksheets.Take(5).Select(w => new WorksheetSummary
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

        ViewBag.RecentMaterials = materials.Take(10).ToList();
        ViewBag.Templates       = templates;

        return View(new GenerateWorksheetViewModel { Profiles = profiles });
    }

    public IActionResult Error() => View();
}
