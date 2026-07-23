# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Forum.Web/Forum.Web.csproj -c Release -o /app

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Seed dữ liệu demo khi khởi động (SeedService chạy khi Development).
ENV ASPNETCORE_ENVIRONMENT=Development
# Render/host cấp cổng qua biến $PORT; mặc định 8080 nếu chạy Docker tay.
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet Forum.Web.dll"]
