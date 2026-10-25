# Obol.Database — builds the dacpac and publishes it to SQL Server, then exits.
# Runs on every deploy (docker compose up): creates the database the first time, applies schema changes afterwards.
# Build context: the solution folder.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Obol.Database/ Obol.Database/
RUN dotnet build Obol.Database/Obol.Database.sqlproj -c Release \
    && mkdir -p /dacpac \
    && cp "$(find Obol.Database/bin -name 'Obol.Database.dacpac' | head -n 1)" /dacpac/

# SqlPackage is a .NET tool; DOTNET_ROLL_FORWARD lets it run on the newest runtime in the image.
FROM mcr.microsoft.com/dotnet/sdk:10.0
ENV PATH="${PATH}:/root/.dotnet/tools" \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_ROLL_FORWARD=Major
RUN dotnet tool install --global microsoft.sqlpackage
WORKDIR /deploy
COPY --from=build /dacpac/Obol.Database.dacpac ./
COPY Obol.Database/Scripts/deploy.sh ./
ENTRYPOINT ["/bin/bash", "/deploy/deploy.sh"]
