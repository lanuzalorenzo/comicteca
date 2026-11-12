FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY .editorconfig ./
COPY src/Comicteca.Web/Comicteca.Web.csproj src/Comicteca.Web/
RUN dotnet restore src/Comicteca.Web/Comicteca.Web.csproj

COPY src/Comicteca.Web/ src/Comicteca.Web/
RUN dotnet publish src/Comicteca.Web/Comicteca.Web.csproj -c Release -o /app/publish --no-restore

FROM nginx:1.30-alpine AS final

# libxslt arrastra vulnerabilidades con corrección en el repositorio de Alpine;
# se sube en la misma capa para que la imagen no salga con CVEs conocidos.
RUN apk upgrade --no-cache libxslt

COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY docker/cabeceras.conf /etc/nginx/snippets/cabeceras.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html

# nginx:alpine arranca como root y baja a `nginx`; aquí el maestro ya corre sin
# privilegios. El pid y la caché van a rutas que ese usuario puede escribir.
RUN sed -i -e 's|^pid .*|pid /tmp/nginx.pid;|' -e '/^user /d' /etc/nginx/nginx.conf \
    && chown -R nginx:nginx /var/cache/nginx

USER nginx

EXPOSE 8080
