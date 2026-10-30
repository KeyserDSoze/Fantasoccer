FROM mcr.microsoft.com/dotnet/sdk:10.0.301 AS build
WORKDIR /source
COPY src/Fantasoccer.Domain/*.csproj src/Fantasoccer.Domain/
COPY src/Fantasoccer.Business/*.csproj src/Fantasoccer.Business/
COPY src/Fantasoccer.Infrastructure.Storage/*.csproj src/Fantasoccer.Infrastructure.Storage/
COPY src/Fantasoccer.Infrastructure.PostgreSql/*.csproj src/Fantasoccer.Infrastructure.PostgreSql/
COPY src/Fantasoccer.Application.Core/*.csproj src/Fantasoccer.Application.Core/
COPY src/Fantasoccer.Applications/Fantasoccer.Application.Api/*.csproj src/Fantasoccer.Applications/Fantasoccer.Application.Api/
RUN dotnet restore src/Fantasoccer.Applications/Fantasoccer.Application.Api/Fantasoccer.Application.Api.csproj
COPY src/Fantasoccer.Domain/ src/Fantasoccer.Domain/
COPY src/Fantasoccer.Business/ src/Fantasoccer.Business/
COPY src/Fantasoccer.Infrastructure.Storage/ src/Fantasoccer.Infrastructure.Storage/
COPY src/Fantasoccer.Infrastructure.PostgreSql/ src/Fantasoccer.Infrastructure.PostgreSql/
COPY src/Fantasoccer.Application.Core/ src/Fantasoccer.Application.Core/
COPY src/Fantasoccer.Applications/Fantasoccer.Application.Api/ src/Fantasoccer.Applications/Fantasoccer.Application.Api/
RUN dotnet publish src/Fantasoccer.Applications/Fantasoccer.Application.Api/Fantasoccer.Application.Api.csproj -c Release --no-restore -o /publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.9 AS runtime
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /publish/ ./
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R app:app /home/app
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Fantasoccer.Application.Api.dll"]
