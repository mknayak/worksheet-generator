using Microsoft.EntityFrameworkCore;
using WorksheetGenerator.Data;
using WorksheetGenerator.Models;

namespace WorksheetGenerator.Services;

public class ProfileStorageService
{
    private readonly AppDbContext _db;

    public ProfileStorageService(AppDbContext db) => _db = db;

    public async Task SaveAsync(StudentProfile profile)
    {
        profile.UpdatedAt = DateTime.Now;

        var exists = await _db.Profiles.AsNoTracking()
                                       .AnyAsync(p => p.Id == profile.Id);
        if (exists)
            _db.Profiles.Update(profile);
        else
            _db.Profiles.Add(profile);

        await _db.SaveChangesAsync();
    }

    public async Task<StudentProfile?> GetAsync(string id)
        => await _db.Profiles.AsNoTracking()
                             .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<List<StudentProfile>> GetAllAsync()
        => await _db.Profiles.AsNoTracking()
                             .OrderByDescending(p => p.CreatedAt)
                             .ToListAsync();

    public bool Delete(string id)
    {
        var p = _db.Profiles.Find(id);
        if (p == null) return false;
        _db.Profiles.Remove(p);
        _db.SaveChanges();
        return true;
    }
}
