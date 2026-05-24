using Microsoft.AspNetCore.Mvc;
using WorksheetGenerator.Models;
using WorksheetGenerator.Services;

namespace WorksheetGenerator.Controllers;

public class ProfileController : Controller
{
    private readonly ProfileStorageService _profileStorage;

    public ProfileController(ProfileStorageService profileStorage)
    {
        _profileStorage = profileStorage;
    }

    public async Task<IActionResult> Index()
    {
        var profiles = await _profileStorage.GetAllAsync();
        return View(profiles);
    }

    [HttpGet]
    public IActionResult Create() => View(new StudentProfile());

    [HttpPost]
    public async Task<IActionResult> Create(StudentProfile profile)
    {
        if (!ModelState.IsValid) return View(profile);

        profile.Id = Guid.NewGuid().ToString();
        profile.CreatedAt = DateTime.Now;
        await _profileStorage.SaveAsync(profile);

        TempData["Success"] = $"Profile for {profile.Name} created!";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var profile = await _profileStorage.GetAsync(id);
        if (profile == null) return NotFound();
        return View(profile);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(StudentProfile profile)
    {
        if (!ModelState.IsValid) return View(profile);

        await _profileStorage.SaveAsync(profile);
        TempData["Success"] = "Profile updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Delete(string id)
    {
        _profileStorage.Delete(id);
        TempData["Success"] = "Profile deleted.";
        return RedirectToAction(nameof(Index));
    }
}
