# Obol.WebApi — the portfolio and the Trading 212 sync. Not published on any port: only the website reaches it.
# Build context: the solution folder.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY Obol.Library/Obol.Library.csproj Obol.Library/
COPY Obol.WebApi/Obol.WebApi.csproj Obol.WebApi/
RUN dotnet restore Obol.WebApi/Obol.WebApi.csproj
COPY Obol.Library/ Obol.Library/
COPY Obol.WebApi/ Obol.WebApi/
# Files copied from a NAS share can keep owner-only permissions; the app runs as the non-root "app" user.
RUN dotnet publish Obol.WebApi/Obol.WebApi.csproj -c Release -o /app --no-restore \
    && chmod -R a+rX /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    TZ=Europe/Lisbon

EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Obol.WebApi.dll"]
