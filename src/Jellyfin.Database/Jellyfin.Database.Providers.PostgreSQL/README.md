# Jellyfin PostgreSQL Database Provider

This project provides a PostgreSQL database provider for Jellyfin, allowing the application to use PostgreSQL as the backend database instead of SQLite.

## Features

- Full PostgreSQL database support
- EF Core integration
- Configuration options for connection strings
- Backup and restore functionality
- Migration support

## Configuration

The PostgreSQL provider can be selected in the Jellyfin configuration by setting the database type to "Jellyfin-PostgreSQL".

## Dependencies

- Npgsql.EntityFrameworkCore.PostgreSQL
- Microsoft.EntityFrameworkCore.Relational
