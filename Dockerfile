# API image for Render (or any Docker host). Build from the backend folder: docker build -t mahosoft-api .

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
# Restore first so the packages stay cached while only the code changes
COPY src/MahoSoft.Entidades/MahoSoft.Entidades.csproj src/MahoSoft.Entidades/
COPY src/MahoSoft.Datos/MahoSoft.Datos.csproj src/MahoSoft.Datos/
COPY src/MahoSoft.Negocio/MahoSoft.Negocio.csproj src/MahoSoft.Negocio/
COPY src/MahoSoft.Api/MahoSoft.Api.csproj src/MahoSoft.Api/
RUN dotnet restore src/MahoSoft.Api/MahoSoft.Api.csproj
COPY src/ src/
RUN dotnet publish src/MahoSoft.Api/MahoSoft.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
# The PDF (QuestPDF) and Excel (ClosedXML) exports need fonts; the base image has none
RUN apt-get update \
    && apt-get install -y --no-install-recommends fontconfig fonts-dejavu-core fonts-liberation2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
# Render sends traffic to port 10000 and ends HTTPS at its proxy: trust its X-Forwarded-For/-Proto
# so HTTPS is detected and login attempts are limited per real client IP
ENV ASPNETCORE_HTTP_PORTS=10000 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
EXPOSE 10000
USER $APP_UID
ENTRYPOINT ["dotnet", "MahoSoft.Api.dll"]
