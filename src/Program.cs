using Microsoft.EntityFrameworkCore;
using WorksheetGenerator.Data;
using WorksheetGenerator.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────
// Add appsettings.local.json for local secrets (file is gitignored)
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

// ── MVC ──────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

// ── SQLite via EF Core ────────────────────────────────────────────────────────
// Database file lives next to the executable (or project root during development)
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "worksheets.db");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddKeyedScoped<IAiWorksheetService, ClaudeAiService>("claude");
builder.Services.AddKeyedScoped<IAiWorksheetService, OpenAiService>("openai");

builder.Services.AddScoped<PdfExtractionService>();
builder.Services.AddScoped<WorksheetStorageService>();
builder.Services.AddScoped<ProfileStorageService>();
builder.Services.AddScoped<SessionMaterialStorageService>();
builder.Services.AddScoped<TemplateStorageService>();

var app = builder.Build();

// ── Ensure database is created on first run ───────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();   // creates worksheets.db + all tables if missing

    // Safe incremental migrations — each statement is idempotent on existing installs.

    // v1: Templates table (added with template feature)
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "Templates" (
            "Id"                  TEXT NOT NULL CONSTRAINT "PK_Templates" PRIMARY KEY,
            "Name"                TEXT NOT NULL,
            "Description"         TEXT NOT NULL,
            "SpecialInstructions" TEXT NOT NULL,
            "CreatedAt"           TEXT NOT NULL,
            "UpdatedAt"           TEXT NOT NULL,
            "QuestionTypes"       TEXT NOT NULL
        )
        """);

    // v2: Difficulty column on Templates (added with difficulty selector)
    // SQLite doesn't support ALTER TABLE … ADD COLUMN IF NOT EXISTS, so we
    // check pragma_table_info first to avoid a noisy EF Core failure log.
    var hasDifficulty = db.Database
        .SqlQueryRaw<string>("SELECT name FROM pragma_table_info('Templates') WHERE name = 'Difficulty'")
        .AsEnumerable()
        .Any();
    if (!hasDifficulty)
        db.Database.ExecuteSqlRaw("""ALTER TABLE "Templates" ADD COLUMN "Difficulty" TEXT NOT NULL DEFAULT 'mixed'""");
}

// ── Ensure upload folder exists (PDF files are not stored, just text, but keep for safety) ──
var env = app.Services.GetRequiredService<IWebHostEnvironment>();
Directory.CreateDirectory(Path.Combine(env.WebRootPath, "uploads"));

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
