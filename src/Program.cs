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

// ── Database ──────────────────────────────────────────────────────────────────
// Set ConnectionStrings:DefaultConnection in appsettings.local.json (or env var)
// to a PostgreSQL connection string to use PostgreSQL in production.
// If unset, falls back to SQLite (good for local development).
var pgConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var usePostgres = !string.IsNullOrWhiteSpace(pgConnectionString);

// Npgsql 6+ by default only accepts UTC DateTimes for timestamptz.
// This switch restores the legacy behaviour so DateTime.Now works without conversion.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

if (usePostgres)
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(pgConnectionString));
}
else
{
    var dbPath = Path.Combine(builder.Environment.ContentRootPath, "worksheets.db");
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite($"Data Source={dbPath}"));
}

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddKeyedScoped<IAiWorksheetService, ClaudeAiService>("claude");
builder.Services.AddKeyedScoped<IAiWorksheetService, OpenAiService>("openai");

builder.Services.AddScoped<PdfExtractionService>();
builder.Services.AddScoped<WorksheetStorageService>();
builder.Services.AddScoped<ProfileStorageService>();
builder.Services.AddScoped<SessionMaterialStorageService>();
builder.Services.AddScoped<TemplateStorageService>();

var app = builder.Build();

// ── Database migrations ───────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var isPostgres = db.Database.ProviderName?.Contains("Npgsql") == true;

    // EnsureCreated creates all tables defined in the model if they don't exist.
    // Safe to call on every startup — no-ops if schema is already present.
    db.Database.EnsureCreated();

    // ── v1: Templates table ───────────────────────────────────────────────────
    // EnsureCreated covers this on fresh installs; the IF NOT EXISTS guard
    // protects existing databases that pre-date the Templates feature.
    if (isPostgres)
    {
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "Templates" (
                "Id"                  TEXT NOT NULL,
                "Name"                TEXT NOT NULL,
                "Description"         TEXT NOT NULL,
                "SpecialInstructions" TEXT NOT NULL,
                "CreatedAt"           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                "UpdatedAt"           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                "QuestionTypes"       TEXT NOT NULL,
                CONSTRAINT "PK_Templates" PRIMARY KEY ("Id")
            )
            """);
    }
    else
    {
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
    }

    // ── v2: Difficulty column on Templates ────────────────────────────────────
    bool hasDifficulty;
    if (isPostgres)
    {
        hasDifficulty = db.Database
            .SqlQueryRaw<string>("""
                SELECT column_name
                FROM information_schema.columns
                WHERE table_name = 'Templates' AND column_name = 'Difficulty'
                """)
            .AsEnumerable()
            .Any();

        if (!hasDifficulty)
            db.Database.ExecuteSqlRaw("""
                ALTER TABLE "Templates"
                ADD COLUMN IF NOT EXISTS "Difficulty" TEXT NOT NULL DEFAULT 'mixed'
                """);
    }
    else
    {
        hasDifficulty = db.Database
            .SqlQueryRaw<string>("SELECT name FROM pragma_table_info('Templates') WHERE name = 'Difficulty'")
            .AsEnumerable()
            .Any();

        if (!hasDifficulty)
            db.Database.ExecuteSqlRaw("""
                ALTER TABLE "Templates" ADD COLUMN "Difficulty" TEXT NOT NULL DEFAULT 'mixed'
                """);
    }
}

// ── Ensure upload folder exists ───────────────────────────────────────────────
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
