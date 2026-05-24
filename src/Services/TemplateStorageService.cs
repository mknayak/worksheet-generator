using Microsoft.EntityFrameworkCore;
using WorksheetGenerator.Data;
using WorksheetGenerator.Models;

namespace WorksheetGenerator.Services;

public class TemplateStorageService
{
    private readonly AppDbContext _db;

    public TemplateStorageService(AppDbContext db) => _db = db;

    public async Task SaveAsync(WorksheetTemplate template)
    {
        template.UpdatedAt = DateTime.Now;
        var exists = await _db.Templates.AsNoTracking()
                                        .AnyAsync(t => t.Id == template.Id);
        if (exists)
            _db.Templates.Update(template);
        else
            _db.Templates.Add(template);

        await _db.SaveChangesAsync();
    }

    public async Task<WorksheetTemplate?> GetAsync(string id)
        => await _db.Templates.AsNoTracking()
                               .FirstOrDefaultAsync(t => t.Id == id);

    public async Task<List<WorksheetTemplate>> GetAllAsync()
        => await _db.Templates.AsNoTracking()
                               .OrderBy(t => t.Name)
                               .ToListAsync();

    public bool Delete(string id)
    {
        var t = _db.Templates.Find(id);
        if (t == null) return false;
        _db.Templates.Remove(t);
        _db.SaveChanges();
        return true;
    }
}
