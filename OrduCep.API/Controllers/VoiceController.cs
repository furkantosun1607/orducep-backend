using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrduCep.Application.Interfaces;
using OrduCep.Application.Services;
using OrduCep.Domain.Entities;
using OrduCep.Domain.Enums;

namespace OrduCep.API.Controllers;

[ApiController]
[Route("api/voice")]
public class VoiceController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IApplicationDbContext _context;
    private readonly IReservationService _reservationService;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public VoiceController(
        IApplicationDbContext context,
        IReservationService reservationService,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _reservationService = reservationService;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost("realtime/call")]
    [Consumes("application/sdp", "text/plain")]
    public async Task<IActionResult> CreateRealtimeCall([FromQuery] string userId, [FromQuery] Guid? ordueviId)
    {
        if (!VoiceAssistantEnabled())
            return StatusCode(503, new { Message = "Sesli asistan şu anda kapalı." });

        var apiKey = _configuration["OPENAI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return StatusCode(503, new { Message = "OPENAI_API_KEY tanımlı olmadığı için sesli asistan başlatılamıyor." });

        var user = await FindUserAsync(userId);
        if (user == null)
            return NotFound(new { Message = "Kullanıcı bulunamadı." });

        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var sdp = await reader.ReadToEndAsync(HttpContext.RequestAborted);
        if (string.IsNullOrWhiteSpace(sdp))
            return BadRequest(new { Message = "WebRTC SDP içeriği boş olamaz." });

        var model = _configuration["OPENAI_REALTIME_MODEL"] ?? "gpt-realtime";
        var voice = _configuration["OPENAI_REALTIME_VOICE"] ?? "sage";
        var sessionConfig = BuildRealtimeSessionConfig(model, voice, ordueviId);

        using var form = new MultipartFormDataContent
        {
            { new StringContent(sdp, Encoding.UTF8, "application/sdp"), "sdp" },
            { new StringContent(sessionConfig, Encoding.UTF8, "application/json"), "session" }
        };

        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/realtime/calls")
        {
            Content = form
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await client.SendAsync(request, HttpContext.RequestAborted);
        var body = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, new { Message = "Realtime oturumu açılamadı.", Detail = body });

        return Content(body, "application/sdp", Encoding.UTF8);
    }

    [HttpPost("tools/execute")]
    public async Task<IActionResult> ExecuteTool([FromBody] VoiceToolRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ToolName))
            return BadRequest(new { Message = "Araç adı zorunludur." });

        var toolName = NormalizeToolName(request.ToolName);

        try
        {
            var result = toolName switch
            {
                "search_orduevis" => await SearchOrduevisAsync(request.Arguments),
                "list_facilities" => await ListFacilitiesAsync(request.Arguments, request.OrdueviId),
                "get_facility_info" => await GetFacilityInfoAsync(request.Arguments),
                "find_slots" => await WithReservableUserAsync(request.UserId, () => FindSlotsAsync(request.Arguments)),
                "lock_reservation" => await WithReservableUserAsync(request.UserId, () => LockReservationAsync(request.Arguments, request.UserId)),
                "confirm_reservation" => await WithReservableUserAsync(request.UserId, () => ConfirmReservationAsync(request.Arguments, request.UserId)),
                "list_my_reservations" => await WithKnownUserAsync(request.UserId, () => ListMyReservationsAsync(request.UserId)),
                _ => new VoiceToolResult(false, $"Bilinmeyen araç: {request.ToolName}", null)
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return Ok(new VoiceToolResult(false, ex.Message, null));
        }
    }

    [HttpPost("phone/session")]
    public async Task<IActionResult> CreatePhoneSession([FromBody] CreateVoiceSessionRequest request)
    {
        var user = await FindUserAsync(request.UserId);
        if (user == null)
            return NotFound(new { Message = "Kullanıcı bulunamadı." });

        var session = new VoiceSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id.ToString(),
            Channel = "phone",
            OrdueviId = request.OrdueviId,
            StateJson = "{}",
            Status = "created",
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30)
        };

        _context.VoiceSessions.Add(session);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(ToVoiceSessionDto(session));
    }

    [HttpPost("text/turn")]
    public async Task<IActionResult> TextTurn([FromBody] VoiceTextTurnRequest request)
    {
        var user = await FindUserAsync(request.UserId);
        if (user == null)
            return NotFound(new { Message = "Kullanıcı bulunamadı." });

        var session = await GetOrCreateTextSessionAsync(request, user.Id.ToString());
        var state = ReadDialogState(session.StateJson);

        if (request.OrdueviId.HasValue)
            await SetOrdueviContextAsync(state, request.OrdueviId.Value);

        var reply = await BuildTextAssistantReplyAsync(state, request.Message, user);

        session.Status = "active";
        session.UpdatedAtUtc = DateTime.UtcNow;
        session.StateJson = JsonSerializer.Serialize(state, JsonOptions);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            SessionId = session.Id,
            Reply = reply
        });
    }

    [HttpPost("phone/start")]
    public async Task<IActionResult> StartPhoneSession([FromBody] VoiceSessionActionRequest request)
    {
        var session = await GetActiveVoiceSessionAsync(request.SessionId);
        if (session == null)
            return NotFound(new { Message = "Telefon oturumu bulunamadı veya süresi doldu." });

        session.Status = "active";
        session.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            session.Id,
            Reply = "OrduCep sesli asistana hoş geldiniz. Hangi orduevi veya hizmet hakkında yardımcı olayım?"
        });
    }

    [HttpPost("phone/turn")]
    public async Task<IActionResult> PhoneTurn([FromBody] VoicePhoneTurnRequest request)
    {
        var session = await GetActiveVoiceSessionAsync(request.SessionId);
        if (session == null)
            return NotFound(new { Message = "Telefon oturumu bulunamadı veya süresi doldu." });

        session.Status = "active";
        session.UpdatedAtUtc = DateTime.UtcNow;
        session.StateJson = JsonSerializer.Serialize(new
        {
            lastTranscript = request.Transcript,
            at = DateTime.UtcNow
        }, JsonOptions);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        var reply = await BuildPhoneFallbackReplyAsync(session, request.Transcript);
        return Ok(new { session.Id, Reply = reply });
    }

    [HttpPost("phone/end")]
    public async Task<IActionResult> EndPhoneSession([FromBody] VoiceSessionActionRequest request)
    {
        var session = await _context.VoiceSessions.FirstOrDefaultAsync(s => s.Id == request.SessionId);
        if (session == null)
            return NotFound(new { Message = "Telefon oturumu bulunamadı." });

        session.Status = "ended";
        session.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new { session.Id, Reply = "Görüşme sonlandırıldı. Sağlıklı günler dileriz." });
    }

    private async Task<VoiceToolResult> SearchOrduevisAsync(JsonElement arguments)
    {
        var query = GetString(arguments, "query");
        if (string.IsNullOrWhiteSpace(query))
            return new VoiceToolResult(false, "Lütfen aramak istediğiniz il, ilçe veya orduevi adını söyleyin.", null);

        var normalized = NormalizeText(query);
        var orduevis = await _context.Orduevleri
            .Where(o => o.Name.Contains(query) || o.Address.Contains(query) || o.Amenities.Contains(query))
            .Take(5)
            .Select(o => new
            {
                o.Id,
                o.Name,
                Location = o.Address,
                o.ContactNumber,
                o.Amenities
            })
            .ToListAsync();

        if (orduevis.Count == 0)
        {
            var all = await _context.Orduevleri
                .Take(250)
                .Select(o => new { o.Id, o.Name, o.Address, o.ContactNumber, o.Amenities })
                .ToListAsync();
            orduevis = all
                .Where(o => NormalizeText($"{o.Name} {o.Address} {o.Amenities}").Contains(normalized))
                .Take(5)
                .Select(o => new
                {
                    o.Id,
                    o.Name,
                    Location = o.Address,
                    o.ContactNumber,
                    o.Amenities
                })
                .ToList();
        }

        return orduevis.Count == 0
            ? new VoiceToolResult(false, "Bu aramaya uygun orduevi bulunamadı.", null)
            : new VoiceToolResult(true, $"{orduevis.Count} orduevi bulundu. En fazla üç seçenek okuyun.", orduevis.Take(3));
    }

    private async Task<VoiceToolResult> ListFacilitiesAsync(JsonElement arguments, Guid? fallbackOrdueviId)
    {
        var ordueviId = GetGuid(arguments, "ordueviId") ?? fallbackOrdueviId;
        if (!ordueviId.HasValue)
            return new VoiceToolResult(false, "Önce hangi orduevi için işlem yapacağınızı söyleyin.", null);

        var facilities = await _context.Facilities
            .Where(f => f.OrdueviId == ordueviId.Value && f.IsActive)
            .OrderBy(f => f.Name)
            .Select(f => new
            {
                f.Id,
                f.Name,
                AppointmentMode = f.AppointmentMode.ToString(),
                OpeningTime = f.OpeningTime.ToString(@"hh\:mm"),
                ClosingTime = f.ClosingTime.ToString(@"hh\:mm"),
                f.Description
            })
            .ToListAsync();

        return facilities.Count == 0
            ? new VoiceToolResult(false, "Bu orduevi için sistemde aktif hizmet bulunamadı.", null)
            : new VoiceToolResult(true, "Aktif hizmetler listelendi.", facilities);
    }

    private async Task<VoiceToolResult> GetFacilityInfoAsync(JsonElement arguments)
    {
        var facilityId = GetGuid(arguments, "facilityId");
        if (!facilityId.HasValue)
            return new VoiceToolResult(false, "Hangi hizmet hakkında bilgi istediğinizi anlayamadım.", null);

        var facility = await _context.Facilities
            .Include(f => f.Services)
            .Include(f => f.Resources)
            .Include(f => f.Orduevi)
            .FirstOrDefaultAsync(f => f.Id == facilityId.Value && f.IsActive);

        if (facility == null)
            return new VoiceToolResult(false, "Bu hizmet sistemde bulunamadı.", null);

        var data = new
        {
            facility.Id,
            facility.Name,
            OrdueviName = facility.Orduevi.Name,
            AppointmentMode = facility.AppointmentMode.ToString(),
            OpeningTime = facility.OpeningTime.ToString(@"hh\:mm"),
            ClosingTime = facility.ClosingTime.ToString(@"hh\:mm"),
            facility.Description,
            Services = facility.Services
                .Where(s => s.IsActive)
                .OrderBy(s => s.ServiceName)
                .Select(s => new { s.Id, s.ServiceName, s.Price, s.DurationMinutes })
                .Take(12),
            Resources = facility.Resources
                .Where(r => r.IsActive)
                .OrderBy(r => r.Name)
                .Select(r => new { r.Id, r.Name })
                .Take(12)
        };

        return new VoiceToolResult(true, "Hizmet bilgisi bulundu.", data);
    }

    private async Task<VoiceToolResult> FindSlotsAsync(JsonElement arguments)
    {
        var facilityId = GetGuid(arguments, "facilityId");
        var date = GetDate(arguments, "date");
        if (!facilityId.HasValue || !date.HasValue)
            return new VoiceToolResult(false, "Müsait saat bakmak için hizmet ve tarih gereklidir.", null);

        var serviceId = GetGuid(arguments, "serviceId");
        var resourceId = GetGuid(arguments, "resourceId");
        var slots = await _reservationService.GetAvailableTimeSlotsAsync(facilityId.Value, date.Value, serviceId, resourceId);
        var available = slots
            .Where(s => s.IsAvailable)
            .Take(3)
            .Select(s => new
            {
                s.StartTime,
                s.EndTime,
                s.AvailableCapacity,
                Label = s.StartTime.ToString("dd.MM.yyyy HH:mm")
            })
            .ToList();

        return available.Count == 0
            ? new VoiceToolResult(false, "Bu tarih için uygun saat bulunamadı.", null)
            : new VoiceToolResult(true, "Uygun saatler bulundu. En fazla üç seçenek okuyun.", available);
    }

    private async Task<VoiceToolResult> LockReservationAsync(JsonElement arguments, string userId)
    {
        var facilityId = GetGuid(arguments, "facilityId");
        var startTime = GetDateTime(arguments, "startTime");
        if (!facilityId.HasValue || !startTime.HasValue)
            return new VoiceToolResult(false, "Randevu kilitlemek için hizmet ve saat gereklidir.", null);

        var success = await _reservationService.LockTimeSlotAsync(
            facilityId.Value,
            startTime.Value,
            GetGuid(arguments, "serviceId"),
            GetGuid(arguments, "resourceId"),
            userId,
            GetInt(arguments, "guestCount") ?? 1);

        return success
            ? new VoiceToolResult(true, "Saat beş dakikalığına sizin için tutuldu. Kesinleştirmek için kullanıcıdan açık onay alın.", new { facilityId, startTime })
            : new VoiceToolResult(false, "Bu saat artık uygun değil. Lütfen başka saat seçin.", null);
    }

    private async Task<VoiceToolResult> ConfirmReservationAsync(JsonElement arguments, string userId)
    {
        var facilityId = GetGuid(arguments, "facilityId");
        var startTime = GetDateTime(arguments, "startTime");
        if (!facilityId.HasValue || !startTime.HasValue)
            return new VoiceToolResult(false, "Randevuyu kesinleştirmek için hizmet ve saat gereklidir.", null);

        var success = await _reservationService.ConfirmReservationAsync(facilityId.Value, startTime.Value, userId);
        return success
            ? new VoiceToolResult(true, "Randevunuz başarıyla oluşturuldu.", new { facilityId, startTime })
            : new VoiceToolResult(false, "Randevu kesinleştirilemedi. Süre dolmuş veya işlem geçersiz olabilir.", null);
    }

    private async Task<VoiceToolResult> ListMyReservationsAsync(string userId)
    {
        var reservations = await _reservationService.GetUserReservationsAsync(userId);
        var result = reservations
            .Where(r => r.Status != "Cancelled")
            .OrderBy(r => r.StartTime)
            .Take(5)
            .Select(r => new
            {
                r.Id,
                r.FacilityName,
                r.ServiceName,
                r.ResourceName,
                r.StartTime,
                r.EndTime,
                r.Status
            });

        return new VoiceToolResult(true, "Randevular listelendi.", result);
    }

    private async Task<VoiceToolResult> WithReservableUserAsync(string userId, Func<Task<VoiceToolResult>> action)
    {
        var user = await FindUserAsync(userId);
        if (user == null)
            return new VoiceToolResult(false, "Kullanıcı bulunamadı.", null);

        if (!PersonnelAccessRules.CanUseFacilities(user.OwnerRank))
            return new VoiceToolResult(false, "Bu hesap askeri tesislerden faydalanamaz; yalnızca sorumlu olduğu birimleri yönetebilir.", null);

        return await action();
    }

    private async Task<VoiceToolResult> WithKnownUserAsync(string userId, Func<Task<VoiceToolResult>> action)
    {
        var user = await FindUserAsync(userId);
        return user == null
            ? new VoiceToolResult(false, "Kullanıcı bulunamadı.", null)
            : await action();
    }

    private async Task<MilitaryIdentityUser?> FindUserAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return null;

        return await _context.MilitaryIdentityUsers.FirstOrDefaultAsync(u => u.Id == userGuid);
    }

    private async Task<VoiceSession?> GetActiveVoiceSessionAsync(Guid sessionId)
    {
        return await _context.VoiceSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.ExpiresAtUtc > DateTime.UtcNow && s.Status != "ended");
    }

    private async Task<VoiceSession> GetOrCreateTextSessionAsync(VoiceTextTurnRequest request, string userId)
    {
        VoiceSession? session = null;

        if (request.SessionId.HasValue)
        {
            session = await _context.VoiceSessions
                .FirstOrDefaultAsync(s =>
                    s.Id == request.SessionId.Value &&
                    s.UserId == userId &&
                    s.Channel == "text" &&
                    s.ExpiresAtUtc > DateTime.UtcNow &&
                    s.Status != "ended");
        }

        if (session != null)
            return session;

        session = new VoiceSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Channel = "text",
            OrdueviId = request.OrdueviId,
            StateJson = "{}",
            Status = "created",
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(45)
        };

        _context.VoiceSessions.Add(session);
        return session;
    }

    private static VoiceDialogState ReadDialogState(string stateJson)
    {
        if (string.IsNullOrWhiteSpace(stateJson))
            return new VoiceDialogState();

        try
        {
            return JsonSerializer.Deserialize<VoiceDialogState>(stateJson, JsonOptions) ?? new VoiceDialogState();
        }
        catch
        {
            return new VoiceDialogState();
        }
    }

    private async Task<string> BuildTextAssistantReplyAsync(VoiceDialogState state, string message, MilitaryIdentityUser user)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Mesajınızı alamadım. Lütfen tekrar yazar mısınız?";

        var normalized = NormalizeText(message);

        if (state.PendingFacilityId.HasValue && IsNegative(normalized))
        {
            ClearPendingReservation(state);
            return "Tamam, randevuyu kesinleştirmedim. Başka bir saat veya hizmet seçebilirsiniz.";
        }

        if (state.PendingFacilityId.HasValue && IsPositiveConfirmation(normalized))
            return await ConfirmPendingReservationAsync(state, user.Id.ToString());

        if (TrySelectSearchResult(state, normalized, out var selectedOrduevi))
        {
            await SetOrdueviContextAsync(state, selectedOrduevi.Id);
            return $"{state.SelectedOrdueviName} seçildi. Hangi hizmet için yardımcı olayım?";
        }

        if (normalized.Contains("randevular"))
            return await FormatUserReservationsAsync(user.Id.ToString());

        if (normalized.Contains("telefon"))
            return await FormatPhoneInfoAsync(state);

        if (LooksLikeOrdueviSearch(normalized) && !LooksLikeReservationIntent(normalized))
            return await HandleOrdueviSearchAsync(state, message);

        if (LooksLikeReservationIntent(normalized) || state.OfferedSlots.Count > 0 || state.SelectedFacilityId.HasValue)
            return await HandleReservationTextAsync(state, message, normalized, user);

        if (!state.SelectedOrdueviId.HasValue)
            return "Size orduevi bulma, tesis bilgisi ve randevu alma konusunda yardımcı olabilirim. Örneğin, Amasra'da orduevi bul yazabilirsiniz.";

        return "Bu orduevi için hizmet bilgisi okuyabilir, uygun saat bakabilir veya randevularınızı listeleyebilirim.";
    }

    private async Task<string> HandleOrdueviSearchAsync(VoiceDialogState state, string message)
    {
        var results = await SearchOrduevisForDialogAsync(message);
        if (results.Count == 0)
            return "Bu aramaya uygun orduevi bulamadım. İl, ilçe veya orduevi adını biraz daha açık yazar mısınız?";

        state.SearchResults = results;

        if (results.Count == 1)
        {
            await SetOrdueviContextAsync(state, results[0].Id);
            return $"{results[0].Name} seçildi. Hangi hizmet için yardımcı olayım?";
        }

        return "Şu seçenekleri buldum: " + string.Join("; ", results.Select((r, i) => $"{i + 1}. {r.Name}")) + ". Hangisini seçmek istersiniz?";
    }

    private async Task<string> HandleReservationTextAsync(VoiceDialogState state, string message, string normalized, MilitaryIdentityUser user)
    {
        if (!PersonnelAccessRules.CanUseFacilities(user.OwnerRank))
            return "Bu hesap askeri tesislerden faydalanamaz; yalnızca sorumlu olduğu birimleri yönetebilir.";

        if (!state.SelectedOrdueviId.HasValue)
        {
            var results = await SearchOrduevisForDialogAsync(message);
            if (results.Count > 0)
            {
                state.SearchResults = results;
                return "Önce orduevini netleştirelim: " + string.Join("; ", results.Select((r, i) => $"{i + 1}. {r.Name}")) + ". Hangisini seçmek istersiniz?";
            }

            return "Randevu için önce hangi orduevi olduğunu yazmanız gerekiyor. Örneğin: Amasra Denizciler Misafirhanesi berber randevusu.";
        }

        var facility = await FindDialogFacilityAsync(state, normalized);
        if (facility == null)
            return await FormatReservableFacilitiesAsync(state.SelectedOrdueviId.Value);

        state.SelectedFacilityId = facility.Id;
        state.SelectedFacilityName = facility.Name;

        var requestedDate = ExtractRequestedDate(message, normalized) ?? state.SelectedDate;
        if (!requestedDate.HasValue)
            return $"{facility.Name} için hangi tarihe bakalım? Örneğin yarın sabah yazabilirsiniz.";

        state.SelectedDate = requestedDate.Value.Date;

        var requestedTime = ExtractRequestedTime(normalized);
        if (requestedTime.HasValue && state.OfferedSlots.Count > 0)
        {
            var offeredSlot = state.OfferedSlots.FirstOrDefault(s => s.StartTime.Hour == requestedTime.Value.Hours && s.StartTime.Minute == requestedTime.Value.Minutes);
            if (offeredSlot != null)
                return await LockPendingReservationAsync(state, user.Id.ToString(), offeredSlot.StartTime);
        }

        var slots = await _reservationService.GetAvailableTimeSlotsAsync(facility.Id, requestedDate.Value.Date);
        var available = slots
            .Where(s => s.IsAvailable)
            .OrderBy(s => s.StartTime)
            .ToList();

        if (requestedTime.HasValue)
        {
            var selectedSlot = available.FirstOrDefault(s => s.StartTime.Hour == requestedTime.Value.Hours && s.StartTime.Minute == requestedTime.Value.Minutes);
            if (selectedSlot == null)
                return $"{requestedDate.Value:dd.MM.yyyy} tarihinde {requestedTime.Value:hh\\:mm} için uygun saat bulamadım. Başka bir saat ister misiniz?";

            return await LockPendingReservationAsync(state, user.Id.ToString(), selectedSlot.StartTime);
        }

        var firstSlots = available.Take(3).ToList();
        if (firstSlots.Count == 0)
            return $"{requestedDate.Value:dd.MM.yyyy} tarihinde {facility.Name} için uygun saat bulamadım. Başka bir tarih deneyelim mi?";

        state.OfferedSlots = firstSlots
            .Select(s => new VoiceOfferedSlot { StartTime = s.StartTime })
            .ToList();

        return $"{facility.Name} için uygun saatler: {string.Join(", ", firstSlots.Select(s => s.StartTime.ToString("HH:mm")))}. Hangisini seçmek istersiniz?";
    }

    private async Task<string> LockPendingReservationAsync(VoiceDialogState state, string userId, DateTime startTime)
    {
        if (!state.SelectedFacilityId.HasValue)
            return "Randevu için önce hizmeti seçmemiz gerekiyor.";

        var success = await _reservationService.LockTimeSlotAsync(state.SelectedFacilityId.Value, startTime, null, null, userId);
        if (!success)
            return "Bu saat artık uygun değil. Lütfen başka bir saat seçin.";

        state.PendingFacilityId = state.SelectedFacilityId;
        state.PendingFacilityName = state.SelectedFacilityName;
        state.PendingStartTime = startTime;
        state.OfferedSlots.Clear();

        return $"{state.PendingFacilityName} için {startTime:dd.MM.yyyy HH:mm} randevusunu onaylıyor musunuz? Lütfen evet veya onaylıyorum yazın.";
    }

    private async Task<string> ConfirmPendingReservationAsync(VoiceDialogState state, string userId)
    {
        if (!state.PendingFacilityId.HasValue || !state.PendingStartTime.HasValue)
            return "Onaylanacak bekleyen bir randevu bulamadım.";

        var facilityName = state.PendingFacilityName;
        var startTime = state.PendingStartTime.Value;
        var success = await _reservationService.ConfirmReservationAsync(state.PendingFacilityId.Value, startTime, userId);
        ClearPendingReservation(state);

        return success
            ? $"{facilityName} için {startTime:dd.MM.yyyy HH:mm} randevunuz oluşturuldu."
            : "Randevu kesinleştirilemedi. Süre dolmuş veya saat artık uygun olmayabilir.";
    }

    private async Task<string> FormatUserReservationsAsync(string userId)
    {
        var reservations = await _reservationService.GetUserReservationsAsync(userId);
        var active = reservations
            .Where(r => r.Status != "Cancelled")
            .OrderBy(r => r.StartTime)
            .Take(3)
            .ToList();

        if (active.Count == 0)
            return "Aktif randevunuz bulunmuyor.";

        return "Randevularınız: " + string.Join("; ", active.Select(r => $"{r.FacilityName} {r.StartTime:dd.MM.yyyy HH:mm}"));
    }

    private async Task<string> FormatPhoneInfoAsync(VoiceDialogState state)
    {
        if (!state.SelectedOrdueviId.HasValue)
            return "Telefon bilgisini okuyabilmem için önce orduevini seçmemiz gerekiyor.";

        var orduevi = await _context.Orduevleri.FirstOrDefaultAsync(o => o.Id == state.SelectedOrdueviId.Value);
        return string.IsNullOrWhiteSpace(orduevi?.ContactNumber)
            ? "Bu orduevinin telefon bilgisi sistemde yok."
            : $"{orduevi.Name} telefon numarası: {orduevi.ContactNumber}.";
    }

    private async Task<string> FormatReservableFacilitiesAsync(Guid ordueviId)
    {
        var facilities = await _context.Facilities
            .Where(f => f.OrdueviId == ordueviId && f.IsActive && f.AppointmentMode != AppointmentMode.WalkInOnly)
            .OrderBy(f => f.Name)
            .Take(5)
            .Select(f => f.Name)
            .ToListAsync();

        return facilities.Count == 0
            ? "Bu orduevi için randevulu birim bulamadım."
            : "Hangi randevulu birim için işlem yapalım? " + string.Join(", ", facilities) + ".";
    }

    private async Task<Facility?> FindDialogFacilityAsync(VoiceDialogState state, string normalized)
    {
        if (!state.SelectedOrdueviId.HasValue)
            return null;

        var facilities = await _context.Facilities
            .Where(f => f.OrdueviId == state.SelectedOrdueviId.Value && f.IsActive && f.AppointmentMode != AppointmentMode.WalkInOnly)
            .OrderBy(f => f.Name)
            .ToListAsync();

        if (facilities.Count == 0)
            return null;

        var matched = facilities.FirstOrDefault(f => FacilityMatchesText(f, normalized));
        if (matched != null)
            return matched;

        if (state.SelectedFacilityId.HasValue)
            return facilities.FirstOrDefault(f => f.Id == state.SelectedFacilityId.Value);

        return facilities.Count == 1 ? facilities[0] : null;
    }

    private static bool FacilityMatchesText(Facility facility, string normalized)
    {
        var haystack = NormalizeText($"{facility.Name} {facility.Description} {facility.Icon}");
        if (normalized.Contains("berber") && haystack.Contains("berber"))
            return true;

        if (normalized.Contains("kuafor") && haystack.Contains("kuafor"))
            return true;

        if ((normalized.Contains("konaklama") || normalized.Contains("misafirhane") || normalized.Contains("oda")) &&
            (haystack.Contains("konaklama") || haystack.Contains("misafirhane")))
            return true;

        if ((normalized.Contains("pide") || normalized.Contains("restoran") || normalized.Contains("yemek")) &&
            (haystack.Contains("pide") || haystack.Contains("restoran") || haystack.Contains("yemek")))
            return true;

        return haystack.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(part => part.Length > 3 && normalized.Contains(part));
    }

    private async Task<List<VoiceSearchResult>> SearchOrduevisForDialogAsync(string message)
    {
        var query = ExtractSearchQuery(message);
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var normalized = NormalizeText(query);
        var all = await _context.Orduevleri
            .Take(250)
            .Select(o => new { o.Id, o.Name, o.Address, o.Amenities })
            .ToListAsync();
        return all
            .Where(o => NormalizeText($"{o.Name} {o.Address} {o.Amenities}").Contains(normalized))
            .Take(3)
            .Select(o => new VoiceSearchResult
            {
                Id = o.Id,
                Name = o.Name,
                Location = o.Address
            })
            .ToList();
    }

    private async Task SetOrdueviContextAsync(VoiceDialogState state, Guid ordueviId)
    {
        var orduevi = await _context.Orduevleri.FirstOrDefaultAsync(o => o.Id == ordueviId);
        if (orduevi == null)
            return;

        state.SelectedOrdueviId = orduevi.Id;
        state.SelectedOrdueviName = orduevi.Name;
        state.SearchResults.Clear();
    }

    private static bool TrySelectSearchResult(VoiceDialogState state, string normalized, out VoiceSearchResult result)
    {
        result = default!;
        if (state.SearchResults.Count == 0)
            return false;

        var index = normalized.Contains("birinci") || Regex.IsMatch(normalized, @"\b1\b")
            ? 0
            : normalized.Contains("ikinci") || Regex.IsMatch(normalized, @"\b2\b")
                ? 1
                : normalized.Contains("ucuncu") || Regex.IsMatch(normalized, @"\b3\b")
                    ? 2
                    : -1;

        if (index >= 0 && index < state.SearchResults.Count)
        {
            result = state.SearchResults[index];
            return true;
        }

        var matched = state.SearchResults.FirstOrDefault(r => NormalizeText(r.Name).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(part => part.Length > 4 && normalized.Contains(part)));

        if (matched == null)
            return false;

        result = matched;
        return true;
    }

    private static string ExtractSearchQuery(string message)
    {
        var cleaned = message
            .Replace("orduevi bul", "", StringComparison.OrdinalIgnoreCase)
            .Replace("orduevi ara", "", StringComparison.OrdinalIgnoreCase)
            .Replace("tesis bul", "", StringComparison.OrdinalIgnoreCase)
            .Replace("tesis ara", "", StringComparison.OrdinalIgnoreCase)
            .Replace("randevu", "", StringComparison.OrdinalIgnoreCase)
            .Replace("berber", "", StringComparison.OrdinalIgnoreCase)
            .Trim(' ', '.', ',', '?', '!');

        cleaned = Regex.Replace(cleaned, @"[’']\s*(da|de|ta|te|daki|deki)\b", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\b([A-Za-zÇĞİÖŞÜçğıöşü]{4,})(da|de|ta|te)\b", "$1", RegexOptions.IgnoreCase);

        return cleaned.Length >= 3 ? cleaned : message.Trim();
    }

    private static DateTime? ExtractRequestedDate(string original, string normalized)
    {
        var today = DateTime.Today;
        if (normalized.Contains("yarin"))
            return today.AddDays(1);

        if (normalized.Contains("bugun"))
            return today;

        var match = Regex.Match(original, @"\b(?<day>\d{1,2})[./-](?<month>\d{1,2})(?:[./-](?<year>\d{2,4}))?\b");
        if (!match.Success)
            return null;

        var day = int.Parse(match.Groups["day"].Value);
        var month = int.Parse(match.Groups["month"].Value);
        var year = match.Groups["year"].Success ? int.Parse(match.Groups["year"].Value) : today.Year;
        if (year < 100)
            year += 2000;

        try
        {
            return new DateTime(year, month, day);
        }
        catch
        {
            return null;
        }
    }

    private static TimeSpan? ExtractRequestedTime(string normalized)
    {
        var match = Regex.Match(normalized, @"\b(?<hour>[01]?\d|2[0-3])[:.,](?<minute>[0-5]\d)\b");
        if (!match.Success)
            return null;

        return new TimeSpan(int.Parse(match.Groups["hour"].Value), int.Parse(match.Groups["minute"].Value), 0);
    }

    private static bool LooksLikeOrdueviSearch(string normalized)
    {
        return normalized.Contains("orduevi") ||
               normalized.Contains("tesis") ||
               normalized.Contains("misafirhane") ||
               normalized.Contains("gazino") ||
               normalized.Contains("amasra") ||
               normalized.Contains("ankara") ||
               normalized.Contains("istanbul") ||
               normalized.Contains("izmir");
    }

    private static bool LooksLikeReservationIntent(string normalized)
    {
        return normalized.Contains("randevu") ||
               normalized.Contains("uygun saat") ||
               normalized.Contains("saat var") ||
               normalized.Contains("berber") ||
               normalized.Contains("kuafor") ||
               normalized.Contains("konaklama");
    }

    private static bool IsPositiveConfirmation(string normalized)
    {
        return normalized.Contains("evet") ||
               normalized.Contains("onayliyorum") ||
               normalized.Contains("onayla") ||
               normalized.Contains("tamam");
    }

    private static bool IsNegative(string normalized)
    {
        return normalized.Contains("hayir") ||
               normalized.Contains("vazgectim") ||
               normalized.Contains("iptal");
    }

    private static void ClearPendingReservation(VoiceDialogState state)
    {
        state.PendingFacilityId = null;
        state.PendingFacilityName = string.Empty;
        state.PendingStartTime = null;
    }

    private async Task<string> BuildPhoneFallbackReplyAsync(VoiceSession session, string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
            return "Sesinizi alamadım. Lütfen tekrar söyler misiniz?";

        var text = NormalizeText(transcript);
        if (text.Contains("randevu") || text.Contains("saat"))
            return "Telefon kanalı hazır. Canlı hat bağlandığında uygun saatleri okuyup açık onayınızla randevuyu kesinleştireceğim.";

        if (text.Contains("telefon") && session.OrdueviId.HasValue)
        {
            var orduevi = await _context.Orduevleri.FirstOrDefaultAsync(o => o.Id == session.OrdueviId.Value);
            return string.IsNullOrWhiteSpace(orduevi?.ContactNumber)
                ? "Bu orduevinin telefon bilgisi sistemde yok."
                : $"{orduevi.Name} telefon numarası: {orduevi.ContactNumber}.";
        }

        return "Size orduevi bulma, hizmet bilgisi okuma ve randevulu birimler için uygun saat bulma konularında yardımcı olabilirim.";
    }

    private bool VoiceAssistantEnabled()
    {
        var value = _configuration["VOICE_ASSISTANT_ENABLED"];
        return string.IsNullOrWhiteSpace(value) || bool.TryParse(value, out var enabled) && enabled;
    }

    private static string BuildRealtimeSessionConfig(string model, string voice, Guid? ordueviId)
    {
        var config = new
        {
            type = "realtime",
            model,
            instructions = $"""
                Sen OrduCep sesli asistanısın. Türkçe, sakin, kısa ve yaşlı kullanıcıya uygun konuş.
                Sistem dışı bilgi uydurma; sadece araçlardan gelen veriye dayan.
                Belirsizlikte en fazla üç seçenek söyle ve netleştirici soru sor.
                T.C. kimlik numarası gibi hassas bilgileri sesli okuma.
                Randevu kesinleştirmeden önce mutlaka kullanıcıdan açık onay al.
                Kullanıcı onaylamadan confirm_reservation aracını çağırma.
                Geçerli orduevi bağlamı: {(ordueviId?.ToString() ?? "seçilmedi")}.
                """,
            audio = new { output = new { voice } },
            tools = BuildToolDefinitions(),
            tool_choice = "auto"
        };

        return JsonSerializer.Serialize(config, JsonOptions);
    }

    private static object[] BuildToolDefinitions()
    {
        return
        [
            Tool("search_orduevis", "İl, ilçe, orduevi adı veya hizmete göre orduevi ara.", new
            {
                type = "object",
                properties = new { query = new { type = "string" } },
                required = new[] { "query" }
            }),
            Tool("list_facilities", "Bir orduevindeki aktif hizmet ve birimleri listele.", new
            {
                type = "object",
                properties = new { ordueviId = new { type = "string", description = "Guid" } }
            }),
            Tool("get_facility_info", "Bir hizmetin saat, fiyat, personel ve açıklama bilgisini getir.", new
            {
                type = "object",
                properties = new { facilityId = new { type = "string", description = "Guid" } },
                required = new[] { "facilityId" }
            }),
            Tool("find_slots", "Randevulu birim için uygun saatleri bul.", new
            {
                type = "object",
                properties = new
                {
                    facilityId = new { type = "string", description = "Guid" },
                    date = new { type = "string", description = "YYYY-MM-DD" },
                    serviceId = new { type = "string", description = "Guid, optional" },
                    resourceId = new { type = "string", description = "Guid, optional" }
                },
                required = new[] { "facilityId", "date" }
            }),
            Tool("lock_reservation", "Kullanıcı açıkça saat seçtiğinde randevuyu beş dakikalığına tut.", new
            {
                type = "object",
                properties = new
                {
                    facilityId = new { type = "string", description = "Guid" },
                    startTime = new { type = "string", description = "ISO datetime" },
                    serviceId = new { type = "string", description = "Guid, optional" },
                    resourceId = new { type = "string", description = "Guid, optional" },
                    guestCount = new { type = "integer" }
                },
                required = new[] { "facilityId", "startTime" }
            }),
            Tool("confirm_reservation", "Kullanıcı açıkça onay verdikten sonra tutulmuş randevuyu kesinleştir.", new
            {
                type = "object",
                properties = new
                {
                    facilityId = new { type = "string", description = "Guid" },
                    startTime = new { type = "string", description = "ISO datetime" }
                },
                required = new[] { "facilityId", "startTime" }
            }),
            Tool("list_my_reservations", "Kullanıcının mevcut randevularını listele.", new
            {
                type = "object",
                properties = new { }
            })
        ];
    }

    private static object Tool(string name, string description, object parameters)
    {
        return new
        {
            type = "function",
            name,
            description,
            parameters
        };
    }

    private static object ToVoiceSessionDto(VoiceSession session)
    {
        return new
        {
            session.Id,
            session.UserId,
            session.Channel,
            session.OrdueviId,
            session.Status,
            session.ExpiresAtUtc
        };
    }

    private static string NormalizeToolName(string value) => value.Trim().Replace("-", "_").ToLowerInvariant();

    private static string GetString(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString() ?? string.Empty;

        return string.Empty;
    }

    private static Guid? GetGuid(JsonElement element, string name)
    {
        var value = GetString(element, name);
        return Guid.TryParse(value, out var guid) ? guid : null;
    }

    private static DateTime? GetDate(JsonElement element, string name)
    {
        var value = GetString(element, name);
        return DateTime.TryParse(value, out var date) ? date.Date : null;
    }

    private static DateTime? GetDateTime(JsonElement element, string name)
    {
        var value = GetString(element, name);
        return DateTime.TryParse(value, out var date) ? date : null;
    }

    private static int? GetInt(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var number))
                return number;

            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out number))
                return number;
        }

        return null;
    }

    private static string NormalizeText(string value)
    {
        return value.Trim()
            .ToLowerInvariant()
            .Replace("ç", "c")
            .Replace("ğ", "g")
            .Replace("ı", "i")
            .Replace("ö", "o")
            .Replace("ş", "s")
            .Replace("ü", "u");
    }
}

public class VoiceToolRequest
{
    public string ToolName { get; set; } = string.Empty;
    public JsonElement Arguments { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid? OrdueviId { get; set; }
}

public record VoiceToolResult(bool Success, string Message, object? Data);

public class VoiceTextTurnRequest
{
    public Guid? SessionId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid? OrdueviId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class CreateVoiceSessionRequest
{
    public string UserId { get; set; } = string.Empty;
    public Guid? OrdueviId { get; set; }
}

public class VoiceSessionActionRequest
{
    public Guid SessionId { get; set; }
}

public class VoicePhoneTurnRequest
{
    public Guid SessionId { get; set; }
    public string Transcript { get; set; } = string.Empty;
}

public class VoiceDialogState
{
    public Guid? SelectedOrdueviId { get; set; }
    public string SelectedOrdueviName { get; set; } = string.Empty;
    public Guid? SelectedFacilityId { get; set; }
    public string SelectedFacilityName { get; set; } = string.Empty;
    public DateTime? SelectedDate { get; set; }
    public Guid? PendingFacilityId { get; set; }
    public string PendingFacilityName { get; set; } = string.Empty;
    public DateTime? PendingStartTime { get; set; }
    public List<VoiceOfferedSlot> OfferedSlots { get; set; } = [];
    public List<VoiceSearchResult> SearchResults { get; set; } = [];
}

public class VoiceOfferedSlot
{
    public DateTime StartTime { get; set; }
}

public class VoiceSearchResult
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}
