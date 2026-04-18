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

        // Azure MySQL Flexible Server SSL ve Pooling Gereksinimleri
        var builder = new MySqlConnector.MySqlConnectionStringBuilder(connectionString)
        {
            SslMode = MySqlConnector.MySqlSslMode.Required,
            MinimumPoolSize = 5,
            MaximumPoolSize = 100,
            ConnectionIdleTimeout = 180,
            TreatTinyAsBoolean = true
        };

        services.AddDbContext<OrduCepDbContext>(options =>
            options.UseMySql(builder.ConnectionString, ServerVersion.AutoDetect(builder.ConnectionString),
                mySqlOptions =>
                {
                    mySqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 10,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                }));
            
        // IApplicationDbContext istendiğinde OrduCepDbContext ver
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<OrduCepDbContext>());

        // Application Services
        services.AddScoped<IReservationService, ReservationService>();

        return services;
    }
}
