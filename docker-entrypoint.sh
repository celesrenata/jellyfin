#!/bin/bash
set -e

# Set PostgreSQL connection BEFORE Jellyfin starts
if [[ -n "$JELLYFIN_DB_TYPE" && "$JELLYFIN_DB_TYPE" == "postgresql" ]]; then
    export JELLYFIN_ConnectionStrings__DefaultConnection="Host=${JELLYFIN_DB_HOST};Port=${JELLYFIN_DB_PORT};Database=${JELLYFIN_DB_NAME};Username=${JELLYFIN_DB_USER};Password=${JELLYFIN_DB_PASSWORD}"
fi

# Use original Jellyfin entrypoint - don't override it
exec /jellyfin/jellyfin --datadir /config --cachedir /cache --ffmpeg /usr/local/bin/rffmpeg
