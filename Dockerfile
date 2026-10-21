FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Zeitwerk.Web/Zeitwerk.Web.csproj src/Zeitwerk.Web/
RUN dotnet restore src/Zeitwerk.Web/Zeitwerk.Web.csproj
COPY src/ src/
RUN dotnet publish src/Zeitwerk.Web/Zeitwerk.Web.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# tzdata: Zeitzonen-Datenbank für Europe/Berlin; libgssapi: von Npgsql beim Verbindungsaufbau gesucht
RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Zeitwerk.Web.dll"]
