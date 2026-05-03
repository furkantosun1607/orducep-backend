namespace OrduCep.Application.Interfaces;

using Microsoft.EntityFrameworkCore;
using OrduCep.Domain.Entities;

public interface IApplicationDbContext
{
    DbSet<Orduevi> Orduevleri { get; set; }
    DbSet<Facility> Facilities { get; set; }
    DbSet<Resource> Resources { get; set; }
    DbSet<FacilityService> FacilityServices { get; set; }
    DbSet<FacilityStaff> FacilityStaffs { get; set; }
    DbSet<Reservation> Reservations { get; set; }
    DbSet<MilitaryIdentityUser> MilitaryIdentityUsers { get; set; }
    DbSet<VoiceSession> VoiceSessions { get; set; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
