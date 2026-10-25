# Obol.Website — the app people open.
# Build context: the solution folder.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY Obol.Library/Obol.Library.csproj Obol.Library/
COPY Obol.Sdk/Obol.Sdk.csproj Obol.Sdk/
COPY Obol.Website/Obol.Website.csproj Obol.Website/
RUN dotnet restore Obol.Website/Obol.Website.csproj
COPY Obol.Library/ Obol.Library/
COPY Obol.Sdk/ Obol.Sdk/
COPY Obol.Website/ Obol.Website/
# Files that reach the build context through a Windows/SMB share or a git checkout can be readable by their owner
# only, and publish copies appsettings.json and wwwroot with those modes: make everything readable for the app user.
RUN dotnet publish Obol.Website/Obol.Website.csproj -c Release -o /app --no-restore \
    && chmod -R a+rX /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# /keys keeps the login cookie keys, so "keep me signed in" survives updates. Owned by the non-root "app" user so a
# named volume inherits that ownership.
RUN mkdir -p /keys && chown "$APP_UID" /keys
VOLUME /keys

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    Website__KeysDirectory=/keys \
    TZ=Europe/Lisbon

EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Obol.Website.dll"]
