# --- Build Stage ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Sadece proje (csproj) dosyalarını kopyalayarak Docker'ın layer-cache mekanizmasından faydalanıyoruz.
# Proje yapısına göre gerekli csproj dosyalarını src altındaki aynı klasörlere kopyalıyoruz.
COPY ["OrduCep.API/OrduCep.API.csproj", "OrduCep.API/"]
COPY ["OrduCep.Application/OrduCep.Application.csproj", "OrduCep.Application/"]
COPY ["OrduCep.Domain/OrduCep.Domain.csproj", "OrduCep.Domain/"]
COPY ["OrduCep.Infrastructure/OrduCep.Infrastructure.csproj", "OrduCep.Infrastructure/"]

# Bağımlılıkları geri yükleme (restore)
RUN dotnet restore "OrduCep.API/OrduCep.API.csproj"

# Kodların tamamını (dockerignore'a takılmayanları) kopyalıyoruz
COPY . .
WORKDIR "/src/OrduCep.API"

# Uygulamayı optimizasyonlu şekilde publish alma
RUN dotnet publish "OrduCep.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# --- Runtime Stage ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Render platformunun HTTP trafiğini dinlediği varsayılan porta yönlendirme
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Build aşamasında oluşan publish dosyalarını çalışma aşamasına aktarma
COPY --from=build /app/publish .

# Uygulama giriş noktasını belirleme
ENTRYPOINT ["dotnet", "OrduCep.API.dll"]
