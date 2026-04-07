using Microsoft.EntityFrameworkCore;
using OrduCep.Application.Interfaces;
using OrduCep.Domain.Entities;

namespace OrduCep.Infrastructure.Persistence;

public class OrduCepDbContext : DbContext, IApplicationDbContext
{
    public OrduCepDbContext(DbContextOptions<OrduCepDbContext> options) : base(options)
    {
    }

    public DbSet<Orduevi> Orduevleri { get; set; } = null!;
    public DbSet<Facility> Facilities { get; set; } = null!;
    public DbSet<Resource> Resources { get; set; } = null!;
    public DbSet<FacilityService> FacilityServices { get; set; } = null!;
    public DbSet<FacilityStaff> FacilityStaffs { get; set; } = null!;
    public DbSet<Reservation> Reservations { get; set; } = null!;
    public DbSet<MilitaryIdentityUser> MilitaryIdentityUsers { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ── Facility → Orduevi ──
        builder.Entity<Facility>()
            .HasOne(f => f.Orduevi)
            .WithMany(o => o.Facilities)
            .HasForeignKey(f => f.OrdueviId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Facility enum dönüşümleri (MySQL uyumu için string olarak sakla) ──
        builder.Entity<Facility>()
            .Property(f => f.Category)
            .HasConversion<string>();

        builder.Entity<Facility>()
            .Property(f => f.AppointmentMode)
            .HasConversion<string>();

        // ── Resource → Facility ──
        builder.Entity<Resource>()
            .HasOne(r => r.Facility)
            .WithMany(f => f.Resources)
            .HasForeignKey(r => r.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Resource>()
            .Property(r => r.Type)
            .HasConversion<string>();

        // ── Reservation → Facility ──
        builder.Entity<Reservation>()
            .HasOne(r => r.Facility)
            .WithMany(f => f.Reservations)
            .HasForeignKey(r => r.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Reservation → Resource (opsiyonel) ──
        builder.Entity<Reservation>()
            .HasOne(r => r.Resource)
            .WithMany(res => res.Reservations)
            .HasForeignKey(r => r.ResourceId)
            .OnDelete(DeleteBehavior.SetNull);

        // ── Reservation → FacilityService (opsiyonel) ──
        builder.Entity<Reservation>()
            .HasOne(r => r.Service)
            .WithMany()
            .HasForeignKey(r => r.ServiceId)
            .OnDelete(DeleteBehavior.SetNull);

        // ── Reservation status enum → string ──
        builder.Entity<Reservation>()
            .Property(r => r.Status)
            .HasConversion<string>();

        // ── FacilityStaff → Facility ──
        builder.Entity<FacilityStaff>()
            .HasOne(fs => fs.Facility)
            .WithMany(f => f.StaffMembers)
            .HasForeignKey(fs => fs.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── FacilityService → Facility ──
        builder.Entity<FacilityService>()
            .HasOne(fs => fs.Facility)
            .WithMany(f => f.Services)
            .HasForeignKey(fs => fs.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Unique index: TC kimlik ──
        builder.Entity<MilitaryIdentityUser>()
            .HasIndex(u => u.IdentityNumber)
            .IsUnique();
    }
}
