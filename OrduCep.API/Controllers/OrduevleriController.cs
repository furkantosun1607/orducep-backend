using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrduCep.API.Services;
using OrduCep.Application.Interfaces;
using OrduCep.Domain.Entities;
using OrduCep.Domain.Enums;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OrduCep.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class OrduevleriController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IGooglePlacesService _googlePlaces;

    public OrduevleriController(IApplicationDbContext context, IGooglePlacesService googlePlaces)
    {
        _context = context;
        _googlePlaces = googlePlaces;
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
                scrapedSourceId = o.ScrapedSourceId,
                slug = o.Slug,
                sourceUrl = o.SourceUrl,
                featuredImageUrl = o.FeaturedImageUrl,
                featuredImageLocalPath = o.FeaturedImageLocalPath,
                amenities = o.Amenities,
                createdAt = o.CreatedAt,
                updatedAt = o.UpdatedAt
            })
            .ToListAsync();

        return Ok(orduevleri);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var orduevi = await _context.Orduevleri
            .Where(o => o.Id == id)
            .Select(o => new
            {
                id = o.Id,
                name = o.Name,
                location = o.Address,
                description = o.Description,
                contactNumber = o.ContactNumber,
                address = o.Address,
                scrapedSourceId = o.ScrapedSourceId,
                slug = o.Slug,
                sourceUrl = o.SourceUrl,
                featuredImageUrl = o.FeaturedImageUrl,
                featuredImageLocalPath = o.FeaturedImageLocalPath,
                amenities = o.Amenities,
                scrapedMetadataJson = o.ScrapedMetadataJson,
                createdAt = o.CreatedAt,
                updatedAt = o.UpdatedAt
            })
            .FirstOrDefaultAsync();

        return orduevi == null
            ? NotFound(new { Message = "Orduevi bulunamadı." })
            : Ok(orduevi);
    }

    [HttpGet("{id:guid}/featured-image")]
    public async Task<IActionResult> GetFeaturedImage(Guid id)
    {
        var image = await _context.Orduevleri
            .Where(o => o.Id == id)
            .Select(o => new
            {
                featuredImageUrl = o.FeaturedImageUrl
            })
            .FirstOrDefaultAsync();

        return image == null
            ? NotFound(new { Message = "Orduevi bulunamadı." })
            : Ok(image);
    }

    [HttpGet("{id:guid}/google-maps")]
    public async Task<IActionResult> GetGoogleMapsDetails(Guid id)
    {
        var orduevi = await _context.Orduevleri.FirstOrDefaultAsync(o => o.Id == id);
        if (orduevi == null)
            return NotFound(new { Message = "Orduevi bulunamadı." });

        try
        {
            var placeId = ReadGooglePlaceId(orduevi.ScrapedMetadataJson);

            if (string.IsNullOrWhiteSpace(placeId))
            {
                var match = await _googlePlaces.FindPlaceIdAsync(orduevi, HttpContext.RequestAborted);
                if (match != null)
                {
                    placeId = match.PlaceId;
                    orduevi.ScrapedMetadataJson = WriteGooglePlaceId(orduevi.ScrapedMetadataJson, match);
                    orduevi.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync(HttpContext.RequestAborted);
                }
            }

            if (string.IsNullOrWhiteSpace(placeId))
                return NotFound(new { Message = "Google Maps eşleşmesi bulunamadı." });

            var details = await _googlePlaces.GetPlaceDetailsAsync(placeId, HttpContext.RequestAborted);
            return details == null
                ? NotFound(new { Message = "Google Maps detayı bulunamadı." })
                : Ok(details);
        }
        catch (InvalidOperationException ex) when (!_googlePlaces.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { ex.Message });
        }
        catch (GooglePlacesApiException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { ex.Message });
        }
    }

    [HttpPost("{id:guid}/google-maps/sync-place-id")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SyncGooglePlaceId(Guid id, [FromQuery] bool force = false)
    {
        var orduevi = await _context.Orduevleri.FirstOrDefaultAsync(o => o.Id == id);
        if (orduevi == null)
            return NotFound(new { Message = "Orduevi bulunamadı." });

        try
        {
            var existingPlaceId = ReadGooglePlaceId(orduevi.ScrapedMetadataJson);
            if (!force && !string.IsNullOrWhiteSpace(existingPlaceId))
            {
                return Ok(new
                {
                    orduevi.Id,
                    orduevi.Name,
                    status = "skipped",
                    placeId = existingPlaceId
                });
            }

            var match = await _googlePlaces.FindPlaceIdAsync(orduevi, HttpContext.RequestAborted);
            if (match == null)
                return NotFound(new { orduevi.Id, orduevi.Name, status = "not_found" });

            orduevi.ScrapedMetadataJson = WriteGooglePlaceId(orduevi.ScrapedMetadataJson, match);
            orduevi.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(HttpContext.RequestAborted);

            return Ok(new
            {
                orduevi.Id,
                orduevi.Name,
                status = "matched",
                placeId = match.PlaceId
            });
        }
        catch (InvalidOperationException ex) when (!_googlePlaces.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { ex.Message });
        }
        catch (GooglePlacesApiException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { ex.Message });
        }
    }

    [HttpPost("google-maps/sync-place-ids")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SyncGooglePlaceIds(
        [FromQuery] bool force = false,
        [FromQuery] int limit = 0,
        [FromQuery] bool verifyDetails = false)
    {
        try
        {
            IQueryable<Orduevi> query = _context.Orduevleri.OrderBy(o => o.Name);

            if (limit > 0)
                query = query.Take(Math.Min(limit, 500));

            var orduevleri = await query.ToListAsync();
            var results = new List<object>();
            var matched = 0;
            var skipped = 0;
            var notFound = 0;
            var withPhotos = 0;
            var withReviews = 0;

            foreach (var orduevi in orduevleri)
            {
                var existingPlaceId = ReadGooglePlaceId(orduevi.ScrapedMetadataJson);
                if (!force && !string.IsNullOrWhiteSpace(existingPlaceId))
                {
                    skipped++;
                    var detailSummary = verifyDetails
                        ? await ReadGoogleDetailSummaryAsync(existingPlaceId, HttpContext.RequestAborted)
                        : null;
                    if (detailSummary?.PhotoCount > 0) withPhotos++;
                    if (detailSummary?.ReviewCount > 0) withReviews++;

                    results.Add(new
                    {
                        orduevi.Id,
                        orduevi.Name,
                        status = "skipped",
                        placeId = existingPlaceId,
                        detailSummary
                    });
                    continue;
                }

                var match = await _googlePlaces.FindPlaceIdAsync(orduevi, HttpContext.RequestAborted);
                if (match == null)
                {
                    notFound++;
                    results.Add(new { orduevi.Id, orduevi.Name, status = "not_found" });
                    continue;
                }

                orduevi.ScrapedMetadataJson = WriteGooglePlaceId(orduevi.ScrapedMetadataJson, match);
                orduevi.UpdatedAt = DateTime.UtcNow;
                matched++;

                var matchedDetailSummary = verifyDetails
                    ? await ReadGoogleDetailSummaryAsync(match.PlaceId, HttpContext.RequestAborted)
                    : null;
                if (matchedDetailSummary?.PhotoCount > 0) withPhotos++;
                if (matchedDetailSummary?.ReviewCount > 0) withReviews++;

                results.Add(new
                {
                    orduevi.Id,
                    orduevi.Name,
                    status = "matched",
                    placeId = match.PlaceId,
                    detailSummary = matchedDetailSummary
                });
            }

            if (matched > 0)
                await _context.SaveChangesAsync(HttpContext.RequestAborted);

            return Ok(new
            {
                total = results.Count,
                matched,
                skipped,
                notFound,
                withPhotos,
                withReviews,
                verifiedDetails = verifyDetails,
                results
            });
        }
        catch (InvalidOperationException ex) when (!_googlePlaces.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { ex.Message });
        }
        catch (GooglePlacesApiException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { ex.Message });
        }
    }

    [HttpGet("google-maps/sync-status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetGoogleMapsSyncStatus()
    {
        var rows = await _context.Orduevleri
            .OrderBy(o => o.Name)
            .Select(o => new
            {
                o.Id,
                o.Name,
                o.ScrapedMetadataJson
            })
            .ToListAsync();

        var items = rows.Select(o =>
        {
            var placeId = ReadGooglePlaceId(o.ScrapedMetadataJson);
            return new
            {
                o.Id,
                o.Name,
                HasPlaceId = !string.IsNullOrWhiteSpace(placeId),
                PlaceId = placeId
            };
        }).ToList();

        return Ok(new
        {
            Total = items.Count,
            WithPlaceId = items.Count(i => i.HasPlaceId),
            MissingPlaceId = items.Count(i => !i.HasPlaceId),
            Items = items
        });
    }

    private async Task<GoogleDetailSummary?> ReadGoogleDetailSummaryAsync(string placeId, CancellationToken cancellationToken)
    {
        var details = await _googlePlaces.GetPlaceDetailsAsync(placeId, cancellationToken);
        return details == null
            ? null
            : new GoogleDetailSummary(details.Photos.Count, details.Reviews.Count, details.Rating, details.UserRatingCount);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateOrdueviRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Address))
        {
            return BadRequest(new { Message = "İsim ve adres zorunludur." });
        }

        var orduevi = new Orduevi
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Address = request.Address.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            ContactNumber = request.ContactNumber?.Trim() ?? string.Empty,
            Slug = request.Slug?.Trim() ?? string.Empty,
            SourceUrl = request.SourceUrl?.Trim() ?? string.Empty,
            FeaturedImageUrl = request.FeaturedImageUrl?.Trim() ?? string.Empty,
            FeaturedImageLocalPath = request.FeaturedImageLocalPath?.Trim() ?? string.Empty,
            Amenities = request.Amenities?.Trim() ?? string.Empty,
            ScrapedMetadataJson = request.ScrapedMetadataJson?.Trim() ?? string.Empty,
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
            scrapedSourceId = orduevi.ScrapedSourceId,
            slug = orduevi.Slug,
            sourceUrl = orduevi.SourceUrl,
            featuredImageUrl = orduevi.FeaturedImageUrl,
            featuredImageLocalPath = orduevi.FeaturedImageLocalPath,
            amenities = orduevi.Amenities,
            scrapedMetadataJson = orduevi.ScrapedMetadataJson,
            createdAt = orduevi.CreatedAt,
            updatedAt = orduevi.UpdatedAt
        });
    }

    /// <summary>
    /// Mevcut bir orduevinin bilgilerini günceller (Admin).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
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

        if (request.Slug != null)
            orduevi.Slug = request.Slug.Trim();

        if (request.SourceUrl != null)
            orduevi.SourceUrl = request.SourceUrl.Trim();

        if (request.FeaturedImageUrl != null)
            orduevi.FeaturedImageUrl = request.FeaturedImageUrl.Trim();

        if (request.FeaturedImageLocalPath != null)
            orduevi.FeaturedImageLocalPath = request.FeaturedImageLocalPath.Trim();

        if (request.Amenities != null)
            orduevi.Amenities = request.Amenities.Trim();

        if (request.ScrapedMetadataJson != null)
            orduevi.ScrapedMetadataJson = request.ScrapedMetadataJson.Trim();

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
            scrapedSourceId = orduevi.ScrapedSourceId,
            slug = orduevi.Slug,
            sourceUrl = orduevi.SourceUrl,
            featuredImageUrl = orduevi.FeaturedImageUrl,
            featuredImageLocalPath = orduevi.FeaturedImageLocalPath,
            amenities = orduevi.Amenities,
            scrapedMetadataJson = orduevi.ScrapedMetadataJson,
            createdAt = orduevi.CreatedAt,
            updatedAt = orduevi.UpdatedAt,
            message = "Orduevi bilgileri başarıyla güncellendi."
        });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
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

    private static string? ReadGooglePlaceId(string metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
            return null;

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (!document.RootElement.TryGetProperty("googleMaps", out var googleMaps))
                return null;

            return googleMaps.TryGetProperty("placeId", out var placeId) && placeId.ValueKind == JsonValueKind.String
                ? placeId.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string WriteGooglePlaceId(string metadataJson, GooglePlaceMatchResult match)
    {
        JsonObject root;

        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            root = new JsonObject();
        }
        else
        {
            try
            {
                root = JsonNode.Parse(metadataJson) as JsonObject
                    ?? new JsonObject { ["legacyMetadata"] = metadataJson };
            }
            catch (JsonException)
            {
                root = new JsonObject { ["legacyMetadata"] = metadataJson };
            }
        }

        root["googleMaps"] = new JsonObject
        {
            ["placeId"] = match.PlaceId,
            ["placeResourceName"] = match.ResourceName,
            ["placeIdQuery"] = match.Query,
            ["placeIdMatchedAtUtc"] = DateTime.UtcNow.ToString("O"),
            ["contentStoragePolicy"] = "Only placeId is persisted; reviews and photos are fetched live from Places API."
        };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    // 2. Bir orduevinin hizmetlerinin (facility) appointmentmodunu çeken endpoint
    [HttpGet("{ordueviId:guid}/facilities/{facilityId:guid}/appointment-mode")]
    public async Task<IActionResult> GetFacilityAppointmentMode(Guid ordueviId, Guid facilityId)
    {
        var facility = await _context.Facilities.FirstOrDefaultAsync(f => f.OrdueviId == ordueviId && f.Id == facilityId);
        if (facility == null) return NotFound(new { Message = "Tesis bulunamadı." });

        return Ok(new { AppointmentMode = facility.AppointmentMode.ToString() });
    }

    // 3. Bir orduevine yeni facility eklenmesi
    [HttpPost("{ordueviId:guid}/facilities")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateFacility(Guid ordueviId, [FromBody] CreateOrdueviFacilityRequest request)
    {
        var ordueviExists = await _context.Orduevleri.AnyAsync(o => o.Id == ordueviId);
        if (!ordueviExists) return NotFound(new { Message = "Orduevi bulunamadı." });

        var facility = new Facility
        {
            Id = Guid.NewGuid(),
            OrdueviId = ordueviId,
            Name = request.Name,
            AppointmentMode = request.AppointmentMode,
            MaxConcurrency = request.Concurrency,
            DefaultSlotDurationMinutes = request.DefaultSlotDurationMinutes,
            OpeningTime = request.OpeningTime,
            ClosingTime = request.ClosingTime,
            Description = request.Description ?? string.Empty,
            ClosedDays = request.ClosedTimes ?? string.Empty,
            IsActive = true
        };

        _context.Facilities.Add(facility);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(facility);
    }

    // 4. Bir orduevinin facilitysinin silinmesi
    [HttpDelete("{ordueviId:guid}/facilities/{facilityId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteFacility(Guid ordueviId, Guid facilityId)
    {
        var facility = await _context.Facilities.FirstOrDefaultAsync(f => f.OrdueviId == ordueviId && f.Id == facilityId);
        if (facility == null) return NotFound(new { Message = "Tesis bulunamadı." });

        _context.Facilities.Remove(facility);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return NoContent();
    }

    // 5. Bir orduevinin facilitysinin hangi günler kapalı olduğunu gösteren endpoint
    [HttpGet("{ordueviId:guid}/facilities/{facilityId:guid}/closed-days")]
    public async Task<IActionResult> GetFacilityClosedDays(Guid ordueviId, Guid facilityId)
    {
        var facility = await _context.Facilities.FirstOrDefaultAsync(f => f.OrdueviId == ordueviId && f.Id == facilityId);
        if (facility == null) return NotFound(new { Message = "Tesis bulunamadı." });

        return Ok(new { ClosedDays = facility.ClosedDays });
    }

    // 6. Bir orduevinin facilitysinin active olup olmadığını belirten endpoint
    [HttpGet("{ordueviId:guid}/facilities/{facilityId:guid}/is-active")]
    public async Task<IActionResult> GetFacilityIsActive(Guid ordueviId, Guid facilityId)
    {
        var facility = await _context.Facilities.FirstOrDefaultAsync(f => f.OrdueviId == ordueviId && f.Id == facilityId);
        if (facility == null) return NotFound(new { Message = "Tesis bulunamadı." });

        return Ok(new { IsActive = facility.IsActive });
    }

    // 7. FacilityService ekleme endpointi
    [HttpPost("{ordueviId:guid}/facilities/{facilityId:guid}/services")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateFacilityService(Guid ordueviId, Guid facilityId, [FromBody] CreateFacilityServiceRequest request)
    {
        var facilityExists = await _context.Facilities.AnyAsync(f => f.OrdueviId == ordueviId && f.Id == facilityId);
        if (!facilityExists) return NotFound(new { Message = "Tesis bulunamadı." });

        var service = new FacilityService
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            ServiceName = request.ServiceName,
            Price = request.Price,
            DurationMinutes = request.DurationMinutes,
            IsActive = true
        };

        _context.FacilityServices.Add(service);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(service);
    }

    // 8. FacilityService silme endpointi
    [HttpDelete("{ordueviId:guid}/facilities/{facilityId:guid}/services/{serviceId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteFacilityService(Guid ordueviId, Guid facilityId, Guid serviceId)
    {
        var facilityExists = await _context.Facilities.AnyAsync(f => f.OrdueviId == ordueviId && f.Id == facilityId);
        if (!facilityExists) return NotFound(new { Message = "Tesis bulunamadı." });

        var service = await _context.FacilityServices.FirstOrDefaultAsync(s => s.FacilityId == facilityId && s.Id == serviceId);
        if (service == null) return NotFound(new { Message = "Hizmet bulunamadı." });

        _context.FacilityServices.Remove(service);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return NoContent();
    }
}

public sealed record GoogleDetailSummary(
    int PhotoCount,
    int ReviewCount,
    double? Rating,
    int? UserRatingCount);

public class CreateOrdueviRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? SourceUrl { get; set; }
    public string? FeaturedImageUrl { get; set; }
    public string? FeaturedImageLocalPath { get; set; }
    public string? Amenities { get; set; }
    public string? ScrapedMetadataJson { get; set; }
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
    public string? Slug { get; set; }
    public string? SourceUrl { get; set; }
    public string? FeaturedImageUrl { get; set; }
    public string? FeaturedImageLocalPath { get; set; }
    public string? Amenities { get; set; }
    public string? ScrapedMetadataJson { get; set; }
}

public class CreateOrdueviFacilityRequest
{
    public string Name { get; set; } = string.Empty;
    public AppointmentMode AppointmentMode { get; set; }
    public int Concurrency { get; set; }
    public int DefaultSlotDurationMinutes { get; set; }
    public TimeSpan OpeningTime { get; set; }
    public TimeSpan ClosingTime { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ClosedTimes { get; set; } = string.Empty;
}

public class CreateFacilityServiceRequest
{
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; }
}
