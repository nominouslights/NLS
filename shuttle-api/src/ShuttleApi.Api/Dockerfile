FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/ShuttleApi.Domain/ShuttleApi.Domain.csproj            src/ShuttleApi.Domain/
COPY src/ShuttleApi.Application/ShuttleApi.Application.csproj  src/ShuttleApi.Application/
COPY src/ShuttleApi.Infrastructure/ShuttleApi.Infrastructure.csproj src/ShuttleApi.Infrastructure/
COPY src/ShuttleApi.Api/ShuttleApi.Api.csproj                  src/ShuttleApi.Api/

RUN dotnet restore "src/ShuttleApi.Api/ShuttleApi.Api.csproj"

COPY src/ src/
RUN dotnet publish "src/ShuttleApi.Api/ShuttleApi.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080

RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
RUN groupadd --system appgroup && useradd --system --gid appgroup appuser
USER appuser

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ShuttleApi.Api.dll"]
