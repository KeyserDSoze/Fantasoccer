FROM mcr.microsoft.com/dotnet/sdk:10.0.301
WORKDIR /source
COPY tools/Fantasoccer.StorageMigration/ tools/Fantasoccer.StorageMigration/
COPY tools/Fantasoccer.DataMigration/ tools/Fantasoccer.DataMigration/
COPY src/Fantasoccer.Domain/ src/Fantasoccer.Domain/
COPY src/Fantasoccer.Application.Core/ src/Fantasoccer.Application.Core/
COPY src/Fantasoccer.Infrastructure.PostgreSql/ src/Fantasoccer.Infrastructure.PostgreSql/
COPY src/Fantasoccer.PostgreSql.Tests/ src/Fantasoccer.PostgreSql.Tests/
COPY src/Fantasoccer.RelationalApi.Tests/ src/Fantasoccer.RelationalApi.Tests/
COPY src/Fantasoccer.Business/ src/Fantasoccer.Business/
COPY src/Fantasoccer.Business.BackgroundJobs/ src/Fantasoccer.Business.BackgroundJobs/
COPY src/Fantasoccer.Infrastructure.Storage/ src/Fantasoccer.Infrastructure.Storage/
COPY src/Fantasoccer.Infrastructure.InMemory/ src/Fantasoccer.Infrastructure.InMemory/
COPY src/Fantasoccer.Applications/Fantasoccer.Application.Api/ src/Fantasoccer.Applications/Fantasoccer.Application.Api/
RUN dotnet restore src/Fantasoccer.PostgreSql.Tests/Fantasoccer.PostgreSql.Tests.csproj && dotnet restore src/Fantasoccer.RelationalApi.Tests/Fantasoccer.RelationalApi.Tests.csproj
ENTRYPOINT ["bash", "-euc", "dotnet test src/Fantasoccer.PostgreSql.Tests/Fantasoccer.PostgreSql.Tests.csproj --no-restore --nologo\ndotnet test src/Fantasoccer.RelationalApi.Tests/Fantasoccer.RelationalApi.Tests.csproj --no-restore --nologo"]
