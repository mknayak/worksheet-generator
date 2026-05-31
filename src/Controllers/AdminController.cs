using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorksheetGenerator.Data;
using WorksheetGenerator.Models;

namespace WorksheetGenerator.Controllers;

public class MigrationResult
{
    public int ProfilesCopied    { get; set; }
    public int MaterialsCopied   { get; set; }
    public int WorksheetsCopied  { get; set; }
    public int TemplatesCopied   { get; set; }
    public int ProfilesSkipped   { get; set; }
    public int MaterialsSkipped  { get; set; }
    public int WorksheetsSkipped { get; set; }
    public int TemplatesSkipped  { get; set; }
    public List<string> Errors   { get; set; } = new();
    public bool IsPostgres        { get; set; }
    public string? SqlitePath     { get; set; }
}

public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        AppDbContext db,
        IConfiguration config,
        IWebHostEnvironment env,
        ILogger<AdminController> logger)
    {
        _db     = db;
        _config  = config;
        _env     = env;
        _logger  = logger;
    }

    // GET /Admin/Migrate — show confirmation page
    public IActionResult Migrate()
    {
        var result = new MigrationResult
        {
            IsPostgres = _db.Database.ProviderName?.Contains("Npgsql") == true,
            SqlitePath = Path.Combine(_env.ContentRootPath, "worksheets.db")
        };
        return View(result);
    }

    // POST /Admin/Migrate — run the migration
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Migrate(string confirm)
    {
        var result = new MigrationResult
        {
            IsPostgres = _db.Database.ProviderName?.Contains("Npgsql") == true,
            SqlitePath = Path.Combine(_env.ContentRootPath, "worksheets.db")
        };

        if (!result.IsPostgres)
        {
            result.Errors.Add("The active database is not PostgreSQL. Configure ConnectionStrings:DefaultConnection first.");
            return View(result);
        }

        if (!System.IO.File.Exists(result.SqlitePath))
        {
            result.Errors.Add($"SQLite file not found at: {result.SqlitePath}");
            return View(result);
        }

        // Open a second DbContext pointing directly at the SQLite file
        var sqliteOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={result.SqlitePath}")
            .Options;

        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        await using var sqlite = new AppDbContext(sqliteOptions);

        try
        {
            // ── Profiles ──────────────────────────────────────────────────────
            var profiles = await sqlite.Profiles.AsNoTracking().ToListAsync();
            var existingProfileIds = (await _db.Profiles.Select(p => p.Id).ToListAsync()).ToHashSet();
            foreach (var p in profiles)
            {
                if (existingProfileIds.Contains(p.Id)) { result.ProfilesSkipped++; continue; }
                _db.Profiles.Add(p);
                result.ProfilesCopied++;
            }
            await _db.SaveChangesAsync();

            // ── Materials ─────────────────────────────────────────────────────
            var materials = await sqlite.Materials.AsNoTracking().ToListAsync();
            var existingMaterialIds = (await _db.Materials.Select(m => m.Id).ToListAsync()).ToHashSet();
            foreach (var m in materials)
            {
                if (existingMaterialIds.Contains(m.Id)) { result.MaterialsSkipped++; continue; }
                _db.Materials.Add(m);
                result.MaterialsCopied++;
            }
            await _db.SaveChangesAsync();

            // ── Worksheets ────────────────────────────────────────────────────
            var worksheets = await sqlite.Worksheets.AsNoTracking().ToListAsync();
            var existingWorksheetIds = (await _db.Worksheets.Select(w => w.Id).ToListAsync()).ToHashSet();
            foreach (var w in worksheets)
            {
                if (existingWorksheetIds.Contains(w.Id)) { result.WorksheetsSkipped++; continue; }
                _db.Worksheets.Add(w);
                result.WorksheetsCopied++;
            }
            await _db.SaveChangesAsync();

            // ── Templates ─────────────────────────────────────────────────────
            var templates = await sqlite.Templates.AsNoTracking().ToListAsync();
            var existingTemplateIds = (await _db.Templates.Select(t => t.Id).ToListAsync()).ToHashSet();
            foreach (var t in templates)
            {
                if (existingTemplateIds.Contains(t.Id)) { result.TemplatesSkipped++; continue; }
                _db.Templates.Add(t);
                result.TemplatesCopied++;
            }
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration error");
            result.Errors.Add($"Migration failed: {ex.Message}");
        }

        return View(result);
    }
}
