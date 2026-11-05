FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/Comicteca.Domain/Comicteca.Domain.csproj src/Comicteca.Domain/
COPY src/Comicteca.Application/Comicteca.Application.csproj src/Comicteca.Application/
COPY src/Comicteca.Infrastructure/Comicteca.Infrastructure.csproj src/Comicteca.Infrastructure/
COPY src/Comicteca.Infrastructure.SqlServer/Comicteca.Infrastructure.SqlServer.csproj src/Comicteca.Infrastructure.SqlServer/
COPY src/Comicteca.Api/Comicteca.Api.csproj src/Comicteca.Api/
RUN dotnet restore src/Comicteca.Api/Comicteca.Api.csproj

COPY src/ src/
COPY data/ data/
RUN dotnet publish src/Comicteca.Api/Comicteca.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /data \
    && chown $APP_UID /data

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["dotnet", "Comicteca.Api.dll"]
