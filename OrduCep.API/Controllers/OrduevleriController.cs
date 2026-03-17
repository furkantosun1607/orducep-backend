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
                contactNumber = o.ContactNumber
            })
            .ToListAsync();

        return Ok(orduevleri);
    }

    public class CreateOrdueviRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
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
            AdminUserId = "admin-123"
        };

        _context.Orduevleri.Add(orduevi);

        // Varsayılan: Berber ve Kantin tesisleri
        var berberTemplate = await _context.FacilityTemplates
            .FirstOrDefaultAsync(t => t.Name == "Berber");

        var kantinTemplate = await _context.FacilityTemplates
            .FirstOrDefaultAsync(t => t.Name == "Kantin");

        if (berberTemplate != null)
        {
            _context.Facilities.Add(new Facility
            {
                Id = Guid.NewGuid(),
                OrdueviId = orduevi.Id,
                FacilityTemplateId = berberTemplate.Id,
                Name = "Erkek Berberi",
                IsAppointmentBased = true,
                OpeningTime = new TimeSpan(8, 30, 0),
                ClosingTime = new TimeSpan(17, 0, 0),
                SlotDurationInMinutes = 15,
                IsActive = true
            });
        }

        if (kantinTemplate != null)
        {
            _context.Facilities.Add(new Facility
            {
                Id = Guid.NewGuid(),
                OrdueviId = orduevi.Id,
                FacilityTemplateId = kantinTemplate.Id,
                Name = "Kantin",
                IsAppointmentBased = false,
                OpeningTime = new TimeSpan(8, 0, 0),
                ClosingTime = new TimeSpan(22, 0, 0),
                SlotDurationInMinutes = 0,
                IsActive = true
            });
        }

        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            id = orduevi.Id,
            name = orduevi.Name,
            location = orduevi.Address,
            description = orduevi.Description,
            contactNumber = orduevi.ContactNumber
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

