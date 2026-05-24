using Microsoft.EntityFrameworkCore;
using WorksheetGenerator.Data;
using WorksheetGenerator.Models;

namespace WorksheetGenerator.Services;

public class WorksheetStorageService
{
    private readonly AppDbContext _db;

    public WorksheetStorageService(AppDbContext db) => _db = db;

    public async Task SaveAsync(Worksheet worksheet)
    {
        var exists = await _db.Worksheets.AsNoTracking()
                                         .AnyAsync(w => w.Id == worksheet.Id);
        if (exists)
            _db.Worksheets.Update(worksheet);
        else
            _db.Worksheets.Add(worksheet);

        await _db.SaveChangesAsync();
    }

    public async Task<Worksheet?> GetAsync(string id)
        => await _db.Worksheets.AsNoTracking()
                               .FirstOrDefaultAsync(w => w.Id == id);

    public async Task<List<Worksheet>> GetAllAsync()
        => await _db.Worksheets.AsNoTracking()
                               .OrderByDescending(w => w.GeneratedAt)
                               .ToListAsync();

    public bool Delete(string id)
    {
        var w = _db.Worksheets.Find(id);
        if (w == null) return false;
        _db.Worksheets.Remove(w);
        _db.SaveChanges();
        return true;
    }
}
