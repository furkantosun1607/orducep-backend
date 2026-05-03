using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using OrduCep.API.Auth;
using OrduCep.API.Services;
using OrduCep.Infrastructure.Persistence;
using OrduCep.API;

EnvFileLoader.Load();

var builder = WebApplication.CreateBuilder(args);

// Tüm servisleri sisteme kayıt
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructure(builder.Configuration); // Gerçek veritabanı (MySQL)
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<IGooglePlacesService, GooglePlacesService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services
    .AddAuthentication(AuthSchemes.Bearer)
    .AddScheme<AuthenticationSchemeOptions, SimpleJwtAuthenticationHandler>(AuthSchemes.Bearer, _ => { });
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    var allowedOrigins = (Environment.GetEnvironmentVariable("ALLOWED_ORIGINS") ??
                          builder.Configuration["ALLOWED_ORIGINS"] ??
                          "http://localhost:4200,http://localhost:4201,http://localhost:4202")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    options.AddPolicy("FrontendOnly",
        policy => policy.WithOrigins(allowedOrigins)
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

var app = builder.Build();

// Veritabanını MySQL üzerinde migrate edip sahte verilerle ayağa kaldıralım
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OrduCepDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

    // Mevcut tablolara yeni kolonları ekle (EnsureCreated bunu yapmıyor)
    await SchemaMigrator.RunAsync(dbContext);

    var seedDataEnabled = app.Configuration.GetValue("SEED_DATA_ENABLED", true);
    if (seedDataEnabled)
        await SeedData.InitializeAsync(scope.ServiceProvider);
    else
        Console.WriteLine("[SeedData] SEED_DATA_ENABLED=false olduğu için başlangıç seed/import adımı atlandı.");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("FrontendOnly");

var scrapedImagesPath = FindDirectoryInParentTree(Path.Combine("scraped_data", "images"));
if (!string.IsNullOrWhiteSpace(scrapedImagesPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(scrapedImagesPath),
        RequestPath = "/scraped_data/images"
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

static string? FindDirectoryInParentTree(string relativePath)
{
    var current = new DirectoryInfo(Directory.GetCurrentDirectory());

    while (current != null)
    {
        var candidate = Path.Combine(current.FullName, relativePath);
        if (Directory.Exists(candidate))
            return candidate;

        current = current.Parent;
    }

    return null;
}
