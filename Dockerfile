# syntax=docker/dockerfile:1

# ---- build ----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
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
# The "-extra" variant, not the plain tag: the plain chiseled image ships with no ICU and
# DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true baked in, and ReferenceOrdering needs real
# tr-TR/en-US collation for every list endpoint's default sort (PLAN.md 3.7) — the plain tag
# throws CultureNotFoundException on first request.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS final
WORKDIR /app
COPY --from=build /app .

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=1

# The datasets live in the image, so readiness depends on nothing external.
ENTRYPOINT ["dotnet", "YaMiSoFt.OpenData.Api.dll"]
