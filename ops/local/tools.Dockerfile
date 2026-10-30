FROM mcr.microsoft.com/dotnet/sdk:10.0.301 AS build
WORKDIR /source
COPY tools/Fantasoccer.StorageMigration/ tools/Fantasoccer.StorageMigration/
COPY tools/Fantasoccer.DataMigration/ tools/Fantasoccer.DataMigration/
COPY tools/Fantasoccer.LegacyModels/ tools/Fantasoccer.LegacyModels/
COPY tools/Fantasoccer.LocalCertificates/ tools/Fantasoccer.LocalCertificates/
COPY src/Fantasoccer.Infrastructure.PostgreSql/ src/Fantasoccer.Infrastructure.PostgreSql/
COPY src/Fantasoccer.Domain/ src/Fantasoccer.Domain/
# Cutover cache seeding reuses the application cache layer and the Blob provider.
COPY src/Fantasoccer.Application.Core/ src/Fantasoccer.Application.Core/
COPY src/Fantasoccer.Infrastructure.Storage/ src/Fantasoccer.Infrastructure.Storage/
RUN dotnet publish tools/Fantasoccer.StorageMigration -c Release -o /storage /p:UseAppHost=false && \
    dotnet publish tools/Fantasoccer.DataMigration -c Release -o /database /p:UseAppHost=false && \
    dotnet publish tools/Fantasoccer.LocalCertificates -c Release -o /certificates-tool /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0.9 AS storage-tools
WORKDIR /app
COPY --from=build /storage/ ./
ENTRYPOINT ["dotnet", "Fantasoccer.StorageMigration.dll"]

# The cache-seeding command reuses Application.Core, which needs the ASP.NET shared framework.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.9 AS database-tools
WORKDIR /app
COPY --from=build /database/ ./
ENTRYPOINT ["dotnet", "Fantasoccer.DataMigration.dll"]

FROM mcr.microsoft.com/dotnet/runtime:10.0.9 AS certificate-tools
WORKDIR /app
COPY --from=build /certificates-tool/ ./
ENTRYPOINT ["dotnet", "Fantasoccer.LocalCertificates.dll"]
