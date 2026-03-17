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
    public DbSet<FacilityTemplate> FacilityTemplates { get; set; } = null!;
    public DbSet<Facility> Facilities { get; set; } = null!;
    public DbSet<FacilityService> FacilityServices { get; set; } = null!;
    public DbSet<FacilityStaff> FacilityStaffs { get; set; } = null!;
    public DbSet<Reservation> Reservations { get; set; } = null!;
    public DbSet<MilitaryIdentityUser> MilitaryIdentityUsers { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Facility ile Orduevi Bağlantısı
        builder.Entity<Facility>()
            .HasOne(f => f.Orduevi)
            .WithMany(o => o.Facilities)
            .HasForeignKey(f => f.OrdueviId)
            .OnDelete(DeleteBehavior.Cascade);

        // Facility ile Şablon (Berber vs) Bağlantısı
        builder.Entity<Facility>()
            .HasOne(f => f.FacilityTemplate)
            .WithMany()
            .HasForeignKey(f => f.FacilityTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        // Randevu - Facility Bağlantısı
        builder.Entity<Reservation>()
            .HasOne(r => r.Facility)
            .WithMany(f => f.Reservations)
            .HasForeignKey(r => r.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        // Facility Staff
        builder.Entity<FacilityStaff>()
            .HasOne(fs => fs.Facility)
            .WithMany(f => f.StaffMembers)
            .HasForeignKey(fs => fs.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);
            
        // Facility Service
        builder.Entity<FacilityService>()
            .HasOne(fs => fs.Facility)
            .WithMany(f => f.Services)
            .HasForeignKey(fs => fs.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MilitaryIdentityUser>()
            .HasIndex(u => u.IdentityNumber)
            .IsUnique();
    }
}
