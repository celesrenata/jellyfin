FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Copy source code
WORKDIR /src
COPY . .

# Build the application
RUN dotnet restore Jellyfin.sln
RUN dotnet build Jellyfin.sln -c Release --no-restore
RUN dotnet publish Jellyfin.Server/Jellyfin.Server.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0

# Install dependencies including SkiaSharp requirements
RUN apt-get update && apt-get install -y \
    curl libpq-dev \
    libfontconfig1 \
    libfreetype6 \
    libharfbuzz0b \
    libicu72 \
    libssl3 \
    gnupg \
    && rm -rf /var/lib/apt/lists/*

# Install jellyfin-ffmpeg from official repository
RUN mkdir -p /etc/apt/keyrings \
    && curl -fsSL https://repo.jellyfin.org/jellyfin_team.gpg.key | gpg --dearmor -o /etc/apt/keyrings/jellyfin.gpg \
    && echo "deb [signed-by=/etc/apt/keyrings/jellyfin.gpg] https://repo.jellyfin.org/debian bookworm main" > /etc/apt/sources.list.d/jellyfin.list \
    && apt-get update \
    && apt-get install -y jellyfin-ffmpeg7 \
    && rm -rf /var/lib/apt/lists/*

# Create jellyfin user
RUN groupadd -r jellyfin && useradd -r -g jellyfin jellyfin

# Create directories
RUN mkdir -p /jellyfin/jellyfin-web /cache /config /media \
    && chown -R jellyfin:jellyfin /jellyfin /cache /config /media

# Copy built application from build stage
COPY --from=build --chown=jellyfin:jellyfin /app/publish/ /jellyfin/

# Copy scripts
COPY --chown=jellyfin:jellyfin docker-entrypoint.sh /usr/local/bin/
COPY --chown=jellyfin:jellyfin rffmpeg /usr/local/bin/rffmpeg
COPY --chown=jellyfin:jellyfin rffprobe /usr/local/bin/rffprobe

# Set permissions
RUN chmod +x /usr/local/bin/docker-entrypoint.sh /usr/local/bin/rffmpeg /usr/local/bin/rffprobe \
    && ln -s /usr/lib/jellyfin-ffmpeg/ffprobe /usr/local/bin/ffprobe

# Expose ports
EXPOSE 8096 8920 7359/udp 1900/udp
ENV POSTGRES_HOST="postgres" \
    POSTGRES_PORT="5432" \
    POSTGRES_DB="jellyfin" \
    POSTGRES_USER="jellyfin" \
    POSTGRES_PASSWORD="jellyfin"

# Set user
USER jellyfin

# Set volumes
VOLUME ["/cache", "/config", "/media"]

ENTRYPOINT ["/usr/local/bin/docker-entrypoint.sh"]
