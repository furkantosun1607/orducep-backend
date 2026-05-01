using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using OrduCep.Infrastructure.Persistence;
using OrduCep.API;

var builder = WebApplication.CreateBuilder(args);

// Tüm servisleri sisteme kayıt
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructure(builder.Configuration); // Gerçek veritabanı (MySQL)

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder.AllowAnyOrigin()
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

    await SeedData.InitializeAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

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
