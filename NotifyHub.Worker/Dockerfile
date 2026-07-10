FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY NotifyHub.sln .
COPY NotifyHub.Api/NotifyHub.Api.csproj NotifyHub.Api/
COPY NotifyHub.Worker/NotifyHub.Worker.csproj NotifyHub.Worker/
COPY NotifyHub.Domain/NotifyHub.Domain.csproj NotifyHub.Domain/
COPY NotifyHub.Infrastructure/NotifyHub.Infrastructure.csproj NotifyHub.Infrastructure/
COPY NotifyHub.Tests/NotifyHub.Domain.Tests/NotifyHub.Domain.Tests.csproj NotifyHub.Tests/NotifyHub.Domain.Tests/
COPY NotifyHub.Tests/NotifyHub.Integration.Tests/NotifyHub.Integration.Tests.csproj NotifyHub.Tests/NotifyHub.Integration.Tests/

RUN dotnet restore NotifyHub.Worker/NotifyHub.Worker.csproj

COPY NotifyHub.Worker/ NotifyHub.Worker/
COPY NotifyHub.Domain/ NotifyHub.Domain/
COPY NotifyHub.Infrastructure/ NotifyHub.Infrastructure/

RUN dotnet publish NotifyHub.Worker/NotifyHub.Worker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet", "NotifyHub.Worker.dll"]
