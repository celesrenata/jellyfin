FROM mcr.microsoft.com/dotnet/aspnet:9.0

# Install dependencies
RUN apt-get update && apt-get install -y \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Create jellyfin user
RUN groupadd -r jellyfin && useradd -r -g jellyfin jellyfin

# Create directories
RUN mkdir -p /jellyfin/jellyfin-web /cache /config /media \
    && chown -R jellyfin:jellyfin /jellyfin /cache /config /media

# Copy application
COPY --chown=jellyfin:jellyfin Jellyfin.Server/bin/Release/net9.0/publish/ /jellyfin/
COPY --chown=jellyfin:jellyfin docker-entrypoint.sh /usr/local/bin/

# Set permissions
RUN chmod +x /usr/local/bin/docker-entrypoint.sh

# Expose ports
EXPOSE 8096 8920 7359/udp 1900/udp

# Set user
USER jellyfin

# Set volumes
VOLUME ["/cache", "/config", "/media"]

ENTRYPOINT ["/usr/local/bin/docker-entrypoint.sh"]
