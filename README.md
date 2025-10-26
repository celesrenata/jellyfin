# Jellyfin with PostgreSQL Docker Setup

A complete Jellyfin media server setup with PostgreSQL database backend, built from source with .NET 9 and jellyfin-ffmpeg 7.1.2.

## Features

- **Jellyfin 10.11.0** built from source with .NET 9
- **PostgreSQL 16** database backend
- **Hardware acceleration** support (VAAPI, QSV, CUDA, etc.)
- **jellyfin-ffmpeg 7.1.2** with full codec support
- **Docker Compose** orchestration
- **Persistent storage** for config, cache, and media

## Quick Start

### Prerequisites
- Docker and Docker Compose
- .NET 9 SDK (for building)

### Build and Run

1. **Build the Jellyfin application**:
   ```bash
   nix-shell -p dotnet-sdk_9 --run "dotnet build Jellyfin.sln"
   nix-shell -p dotnet-sdk_9 --run "dotnet publish Jellyfin.Server/Jellyfin.Server.csproj -c Release -o Jellyfin.Server/bin/Release/net9.0/publish/"
   ```

2. **Start the services**:
   ```bash
   docker compose up -d
   ```

3. **Access Jellyfin**:
   - Web UI: http://localhost:8096
   - Complete the setup wizard

### Services

- **Jellyfin**: Port 8096 (HTTP), 8920 (HTTPS), 7359/UDP, 1900/UDP
- **PostgreSQL**: Port 5432

### Storage

- **Config**: `jellyfin_config` volume
- **Cache**: `jellyfin_cache` volume  
- **Database**: `postgres_data` volume
- **Media**: `./media` directory (create and add your media files)

## Configuration

### Environment Variables

The following environment variables are automatically configured:

- `JELLYFIN_DB_TYPE=postgresql`
- `JELLYFIN_DB_HOST=postgres`
- `JELLYFIN_DB_PORT=5432`
- `JELLYFIN_DB_NAME=jellyfin`
- `JELLYFIN_DB_USER=jellyfin`
- `JELLYFIN_DB_PASSWORD=jellyfin`

### FFmpeg

- **Path**: `/usr/local/bin/rffmpeg` (proxies to `/usr/lib/jellyfin-ffmpeg/ffmpeg`)
- **Version**: 7.1.2-3-bookworm
- **Hardware Acceleration**: Full support for Intel QSV, NVIDIA CUDA, AMD VAAPI

## Development

### Building from Source

The Dockerfile builds Jellyfin from the current source code:

1. Installs .NET 9 runtime and dependencies
2. Installs jellyfin-ffmpeg from official repository
3. Copies published application
4. Creates rffmpeg/rffprobe proxy scripts
5. Configures PostgreSQL connection

### Logs

```bash
# View Jellyfin logs
docker compose logs jellyfin -f

# View PostgreSQL logs
docker compose logs postgres -f

# View all logs
docker compose logs -f
```

### Database Access

```bash
# Connect to PostgreSQL
docker compose exec postgres psql -U jellyfin -d jellyfin
```

## Troubleshooting

### Common Issues

1. **FFmpeg not found**: Ensure jellyfin-ffmpeg7 is properly installed
2. **Database connection failed**: Check PostgreSQL container health
3. **Permission errors**: Verify volume permissions and user configuration

### Reset Database

```bash
docker compose down
docker volume rm jellyfin_postgres_data
docker compose up -d
```

### Clean Rebuild

```bash
docker compose down
docker compose build --no-cache
docker compose up -d
```

## Architecture

```
┌─────────────────┐    HTTP/API    ┌─────────────────┐
│   Web Browser   │ ──────────────▶ │   Jellyfin      │
│   (Port 8096)   │                │   Container     │
└─────────────────┘                │                 │
                                   │   - .NET 9      │
                                   │   - FFmpeg 7.1  │
                                   │   - rffmpeg     │
                                   └─────────┬───────┘
                                             │ SQL
                                             ▼
                                   ┌─────────────────┐
                                   │   PostgreSQL    │
                                   │   Container     │
                                   │   (Port 5432)   │
                                   └─────────────────┘
```

## License

This project follows the same license as Jellyfin (GPL-2.0).
