using Microsoft.AspNetCore.Mvc;
using WorksheetGenerator.Models;
using WorksheetGenerator.Services;

namespace WorksheetGenerator.Controllers;

public class TemplateController : Controller
{
    private readonly TemplateStorageService _templates;

    public TemplateController(TemplateStorageService templates)
        => _templates = templates;

    // GET /Template
    public async Task<IActionResult> Index()
    {
        var list = await _templates.GetAllAsync();
        return View(list);
    }

    // GET /Template/Create
    public IActionResult Create()
        => View(new WorksheetTemplate());

    // POST /Template/Create
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string   name,
        string   description,
        string   specialInstructions,
        string   difficulty,
        string[] questionType,
        int[]    questionCount,
        int[]    questionPoints)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Template name is required.";
            return RedirectToAction(nameof(Create));
        }

        var template = new WorksheetTemplate
        {
            Id                  = Guid.NewGuid().ToString(),
            Name                = name.Trim(),
            Description         = description?.Trim() ?? string.Empty,
            SpecialInstructions = specialInstructions?.Trim() ?? string.Empty,
            Difficulty          = ValidateDifficulty(difficulty),
            CreatedAt           = DateTime.Now,
            UpdatedAt           = DateTime.Now,
            QuestionTypes       = BuildQuestionTypes(questionType, questionCount, questionPoints)
        };

        if (!template.QuestionTypes.Any())
        {
            TempData["Error"] = "Add at least one question type.";
            return RedirectToAction(nameof(Create));
        }

        await _templates.SaveAsync(template);
        TempData["Success"] = $"Template \"{template.Name}\" created.";
        return RedirectToAction(nameof(Index));
    }

    // GET /Template/Edit/{id}
    public async Task<IActionResult> Edit(string id)
    {
        var t = await _templates.GetAsync(id);
        if (t == null) return NotFound();
        return View(t);
    }

    // POST /Template/Edit/{id}
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string   id,
        string   name,
        string   description,
        string   specialInstructions,
        string   difficulty,
        string[] questionType,
        int[]    questionCount,
        int[]    questionPoints)
    {
        var template = await _templates.GetAsync(id);
        if (template == null) return NotFound();

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Template name is required.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        template.Name                = name.Trim();
        template.Description         = description?.Trim() ?? string.Empty;
        template.SpecialInstructions = specialInstructions?.Trim() ?? string.Empty;
        template.Difficulty          = ValidateDifficulty(difficulty);
        template.QuestionTypes       = BuildQuestionTypes(questionType, questionCount, questionPoints);

        if (!template.QuestionTypes.Any())
        {
            TempData["Error"] = "Add at least one question type.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        await _templates.SaveAsync(template);
        TempData["Success"] = $"Template \"{template.Name}\" updated.";
        return RedirectToAction(nameof(Index));
    }

    // POST /Template/Delete
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Delete(string id)
    {
        _templates.Delete(id);
        TempData["Success"] = "Template deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string ValidateDifficulty(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "easy"   => "easy",
            "medium" => "medium",
            "hard"   => "hard",
            _        => "mixed"
        };

    private static List<QuestionTypeConfig> BuildQuestionTypes(
        string[] types, int[] counts, int[] points)
    {
        var result = new List<QuestionTypeConfig>();
        int len = Math.Min(types.Length, Math.Min(counts.Length, points.Length));
        for (int i = 0; i < len; i++)
        {
            if (string.IsNullOrWhiteSpace(types[i])) continue;
            if (counts[i] <= 0) continue;
            result.Add(new QuestionTypeConfig
            {
                Type   = types[i],
                Count  = counts[i],
                Points = Math.Max(1, points[i])
            });
        }
        return result;
    }
}
