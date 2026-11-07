# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-preview.6@sha256:21583a8eeae95b29bb323948e984f387d7d990919a87a1a88e360295166ffcaa AS build
WORKDIR /src
COPY . .
RUN --mount=type=secret,id=nuget_token,env=NUGET_TOKEN \
    cp NuGet.config /tmp/NuGet.config \
    && dotnet nuget update source eliferpg-github --username docker --password "$NUGET_TOKEN" \
       --store-password-in-clear-text --configfile /tmp/NuGet.config \
    && dotnet restore src/Api/Api.csproj --configfile /tmp/NuGet.config \
    && rm /tmp/NuGet.config
RUN dotnet publish src/Api/Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-preview.6@sha256:80b7acce1eee1faeec905a084fedfa7d99a3b2dbd5d09a2c51f15a55022e9772
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "ELifeRPG.Bridge.Api.dll"]
