using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OrduCep.Infrastructure.Persistence;
using OrduCep.Application.Interfaces;
using OrduCep.Application.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<OrduCepDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
            
        // IApplicationDbContext istendiğinde OrduCepDbContext ver
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<OrduCepDbContext>());

        // Application Services
        services.AddScoped<IReservationService, ReservationService>();

        return services;
    }
}
