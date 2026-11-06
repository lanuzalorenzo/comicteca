FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY .editorconfig ./
COPY src/Comicteca.Web/Comicteca.Web.csproj src/Comicteca.Web/
RUN dotnet restore src/Comicteca.Web/Comicteca.Web.csproj

COPY src/Comicteca.Web/ src/Comicteca.Web/
RUN dotnet publish src/Comicteca.Web/Comicteca.Web.csproj -c Release -o /app/publish --no-restore

FROM nginx:1.27-alpine AS final

COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html

EXPOSE 80
