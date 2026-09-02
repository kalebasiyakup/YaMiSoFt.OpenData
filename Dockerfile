# syntax=docker/dockerfile:1

# ---- build ----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore against the project files alone so that editing source or data does not
# invalidate the package layer.
COPY Directory.Build.props ./
COPY src/YaMiSoFt.OpenData.Core/YaMiSoFt.OpenData.Core.csproj src/YaMiSoFt.OpenData.Core/
COPY src/YaMiSoFt.OpenData.Data/YaMiSoFt.OpenData.Data.csproj src/YaMiSoFt.OpenData.Data/
COPY src/YaMiSoFt.OpenData.Api/YaMiSoFt.OpenData.Api.csproj src/YaMiSoFt.OpenData.Api/
RUN dotnet restore src/YaMiSoFt.OpenData.Api/YaMiSoFt.OpenData.Api.csproj

COPY src/ src/
COPY data/ data/

RUN dotnet publish src/YaMiSoFt.OpenData.Api/YaMiSoFt.OpenData.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app

# ---- runtime --------------------------------------------------------------
# Chiseled: no shell, no package manager, runs as a non-root user by default.
# The API has no native dependencies.
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app .

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=1

# The datasets live in the image, so readiness depends on nothing external.
ENTRYPOINT ["dotnet", "YaMiSoFt.OpenData.Api.dll"]
