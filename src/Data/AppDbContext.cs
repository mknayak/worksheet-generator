using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WorksheetGenerator.Models;

namespace WorksheetGenerator.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<StudentProfile>    Profiles  { get; set; }
    public DbSet<SessionMaterial>   Materials { get; set; }
    public DbSet<Worksheet>         Worksheets{ get; set; }
    public DbSet<WorksheetTemplate> Templates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── StudentProfile ───────────────────────────────────────────────
        modelBuilder.Entity<StudentProfile>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).ValueGeneratedNever(); // we assign Guids ourselves
        });

        // ── SessionMaterial ──────────────────────────────────────────────
        modelBuilder.Entity<SessionMaterial>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Id).ValueGeneratedNever();
            // ExtractedText can be many KB — no length restriction
            e.Property(m => m.ExtractedText).HasColumnType("TEXT");
        });

        // ── Worksheet ────────────────────────────────────────────────────
        modelBuilder.Entity<Worksheet>(e =>
        {
            e.HasKey(w => w.Id);
            e.Property(w => w.Id).ValueGeneratedNever();

            // Persist the Questions list as a JSON blob in a single TEXT column.
            // WorksheetQuestion is not a separate table — it's always loaded with
            // its parent Worksheet, which is how the app accesses it.
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var questionsComparer = new ValueComparer<List<WorksheetQuestion>>(
                (a, b) => JsonSerializer.Serialize(a, jsonOptions) == JsonSerializer.Serialize(b, jsonOptions),
                v      => JsonSerializer.Serialize(v, jsonOptions).GetHashCode(),
                v      => JsonSerializer.Deserialize<List<WorksheetQuestion>>(
                               JsonSerializer.Serialize(v, jsonOptions), jsonOptions) ?? new()
            );

            e.Property(w => w.Questions)
             .HasColumnType("TEXT")
             .HasConversion(
                 v  => JsonSerializer.Serialize(v, jsonOptions),
                 v  => JsonSerializer.Deserialize<List<WorksheetQuestion>>(v, jsonOptions) ?? new(),
                 questionsComparer
             );

            // TotalPoints is [NotMapped] — no column needed
        });

        // ── WorksheetTemplate ────────────────────────────────────────────
        modelBuilder.Entity<WorksheetTemplate>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).ValueGeneratedNever();

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var questionTypesComparer = new ValueComparer<List<QuestionTypeConfig>>(
                (a, b) => JsonSerializer.Serialize(a, jsonOptions) == JsonSerializer.Serialize(b, jsonOptions),
                v      => JsonSerializer.Serialize(v, jsonOptions).GetHashCode(),
                v      => JsonSerializer.Deserialize<List<QuestionTypeConfig>>(
                               JsonSerializer.Serialize(v, jsonOptions), jsonOptions) ?? new()
            );

            e.Property(t => t.QuestionTypes)
             .HasColumnType("TEXT")
             .HasConversion(
                 v => JsonSerializer.Serialize(v, jsonOptions),
                 v => JsonSerializer.Deserialize<List<QuestionTypeConfig>>(v, jsonOptions) ?? new(),
                 questionTypesComparer
             );

            // TotalQuestions and TotalPoints are computed — not stored
            e.Ignore(t => t.TotalQuestions);
            e.Ignore(t => t.TotalPoints);
        });
    }
}
