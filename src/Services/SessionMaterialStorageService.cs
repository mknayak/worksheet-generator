using Microsoft.EntityFrameworkCore;
using WorksheetGenerator.Data;
using WorksheetGenerator.Models;

namespace WorksheetGenerator.Services;

public class SessionMaterialStorageService
{
    private readonly AppDbContext _db;

    public SessionMaterialStorageService(AppDbContext db) => _db = db;

    public async Task SaveAsync(SessionMaterial material)
    {
        var exists = await _db.Materials.AsNoTracking()
                                        .AnyAsync(m => m.Id == material.Id);
        if (exists)
            _db.Materials.Update(material);
        else
            _db.Materials.Add(material);

        await _db.SaveChangesAsync();
    }

    public async Task<SessionMaterial?> GetAsync(string id)
        => await _db.Materials.AsNoTracking()
                              .FirstOrDefaultAsync(m => m.Id == id);

    public async Task<List<SessionMaterial>> GetAllAsync()
        => await _db.Materials.AsNoTracking()
                              .OrderByDescending(m => m.UploadedAt)
                              .ToListAsync();

    public bool Delete(string id)
    {
        var m = _db.Materials.Find(id);
        if (m == null) return false;
        _db.Materials.Remove(m);
        _db.SaveChanges();
        return true;
    }
}
