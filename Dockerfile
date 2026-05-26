# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder
WORKDIR /source

COPY . .
RUN dotnet publish -c Release -o /app

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# ImageMagick + Ghostscript for PDF -> PNG conversion (markup feature).
# Debian's ImageMagick ships with PDF rights disabled by default; relax that
# so the app can call `convert input.pdf[0] output.png`.
RUN apt-get update \
    && apt-get install -y --no-install-recommends imagemagick ghostscript \
    && rm -rf /var/lib/apt/lists/* \
    && sed -i 's|<policy domain="coder" rights="none" pattern="PDF" />|<policy domain="coder" rights="read\|write" pattern="PDF" />|' /etc/ImageMagick-6/policy.xml || true

COPY --from=builder /app .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "FireStopEvacTracker.dll"]
