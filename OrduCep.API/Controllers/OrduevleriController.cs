using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrduCep.Application.Interfaces;
using OrduCep.Domain.Entities;

namespace OrduCep.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrduevleriController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public OrduevleriController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var orduevleri = await _context.Orduevleri
            .Select(o => new
            {
                id = o.Id,
                name = o.Name,
                location = o.Address,
                description = o.Description,
                contactNumber = o.ContactNumber,
                address = o.Address,
                createdAt = o.CreatedAt,
                updatedAt = o.UpdatedAt
            })
            .ToListAsync();

        return Ok(orduevleri);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrdueviRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Location))
        {
            return BadRequest(new { Message = "İsim ve konum zorunludur." });
        }

        var orduevi = new Orduevi
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Address = request.Location.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            ContactNumber = request.ContactNumber?.Trim() ?? string.Empty,
            AdminUserId = "admin-123",
            CreatedAt = DateTime.UtcNow
        };

        _context.Orduevleri.Add(orduevi);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            id = orduevi.Id,
            name = orduevi.Name,
            location = orduevi.Address,
            description = orduevi.Description,
            contactNumber = orduevi.ContactNumber,
            address = orduevi.Address,
            createdAt = orduevi.CreatedAt,
            updatedAt = orduevi.UpdatedAt
        });
    }

    /// <summary>
    /// Mevcut bir orduevinin bilgilerini günceller (Admin).
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrdueviRequest request)
    {
        var orduevi = await _context.Orduevleri.FirstOrDefaultAsync(o => o.Id == id);
        if (orduevi == null)
            return NotFound(new { Message = "Orduevi bulunamadı." });

        if (!string.IsNullOrWhiteSpace(request.Name))
            orduevi.Name = request.Name.Trim();

        if (request.Location != null)
            orduevi.Address = request.Location.Trim();

        if (request.Address != null)
            orduevi.Address = request.Address.Trim();

        if (request.Description != null)
            orduevi.Description = request.Description.Trim();

        if (request.ContactNumber != null)
            orduevi.ContactNumber = request.ContactNumber.Trim();

        orduevi.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            id = orduevi.Id,
            name = orduevi.Name,
            location = orduevi.Address,
            description = orduevi.Description,
            contactNumber = orduevi.ContactNumber,
            address = orduevi.Address,
            createdAt = orduevi.CreatedAt,
            updatedAt = orduevi.UpdatedAt,
            message = "Orduevi bilgileri başarıyla güncellendi."
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var orduevi = await _context.Orduevleri
            .FirstOrDefaultAsync(o => o.Id == id);

        if (orduevi == null)
        {
            return NotFound(new { Message = "Orduevi bulunamadı." });
        }

        _context.Orduevleri.Remove(orduevi);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return NoContent();
    }
}

public class CreateOrdueviRequest
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
}

public class UpdateOrdueviRequest
{
    public string? Name { get; set; }
    /// <summary>Şehir / bölge bilgisi (örn: "Kızılay, Ankara")</summary>
    public string? Location { get; set; }
    /// <summary>Açık adres (örn: "Atatürk Bulvarı No:1"). Location ile aynı DB alanını günceller.</summary>
    public string? Address { get; set; }
    public string? Description { get; set; }
    public string? ContactNumber { get; set; }
}
