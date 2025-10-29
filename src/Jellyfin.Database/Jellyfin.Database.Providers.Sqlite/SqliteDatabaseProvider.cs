using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.DbConfiguration;
using MediaBrowser.Common.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace Jellyfin.Database.Providers.Sqlite;

/// <summary>
/// Configures jellyfin to use an SQLite database.
/// </summary>
[JellyfinDatabaseProviderKey("Jellyfin-SQLite")]
public sealed class SqliteDatabaseProvider : IJellyfinDatabaseProvider
{
    private const string BackupFolderName = "SQLiteBackups";
    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger<SqliteDatabaseProvider> _logger;
    private IDbContextFactory<JellyfinDbContext>? _dbContextFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteDatabaseProvider"/> class.
    /// </summary>
    /// <param name="applicationPaths">Service to construct the fallback when the old data path configuration is used.</param>
    /// <param name="logger">A logger.</param>
    public SqliteDatabaseProvider(IApplicationPaths applicationPaths, ILogger<SqliteDatabaseProvider> logger)
    {
        _applicationPaths = applicationPaths;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IDbContextFactory<JellyfinDbContext>? DbContextFactory
    {
        get => _dbContextFactory;
        set
        {
            _dbContextFactory = value;
            if (_dbContextFactory != null)
            {
                Console.WriteLine("=== DBCONTEXTFACTORY SET - RUNNING SQLMITE MIGRATION ===");
                Task.Run(async () =>
                {
                    try
                    {
                        var pgConnectionString = $"Host={Environment.GetEnvironmentVariable("JELLYFIN_DB_HOST") ?? "postgres"};Port={Environment.GetEnvironmentVariable("JELLYFIN_DB_PORT") ?? "5432"};Database={Environment.GetEnvironmentVariable("JELLYFIN_DB_NAME") ?? "jellyfin"};Username={Environment.GetEnvironmentVariable("JELLYFIN_DB_USER") ?? "jellyfin"};Password={Environment.GetEnvironmentVariable("JELLYFIN_DB_PASSWORD") ?? "jellyfin"}";

                        // Migrate existing SQLite database to PostgreSQL
                        SqliteSchemaReader.MigrateSqliteToPostgreSQL("/config/data/jellyfin.db", pgConnectionString);

                        using var context = await _dbContextFactory.CreateDbContextAsync().ConfigureAwait(false);
                        Console.WriteLine("=== RUNNING MIGRATIONS WITH POSTGRESQL BACKEND ===");
                        await context.Database.MigrateAsync().ConfigureAwait(false);
                        Console.WriteLine("=== SQLMITE MIGRATION COMPLETED ===");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"=== SQLMITE MIGRATION ERROR: {ex.Message} ===");
                        _logger.LogError(ex, "Failed to run SQLMite migration");
                    }
                });
            }
        }
    }

    /// <inheritdoc/>
    public void Initialise(DbContextOptionsBuilder options, DatabaseConfigurationOptions databaseConfiguration)
    {
        static T? GetOption<T>(ICollection<CustomDatabaseOption>? options, string key, Func<string, T> converter, Func<T>? defaultValue = null)
        {
            if (options is null)
            {
                return defaultValue is not null ? defaultValue() : default;
            }

            var value = options.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (value is null)
            {
                return defaultValue is not null ? defaultValue() : default;
            }

            return converter(value.Value);
        }

        var customOptions = databaseConfiguration.CustomProviderOptions?.Options;

        var sqliteConnectionBuilder = new SqliteConnectionStringBuilder();
        sqliteConnectionBuilder.DataSource = Path.Combine(_applicationPaths.DataPath, "jellyfin.db");
        sqliteConnectionBuilder.Cache = GetOption(customOptions, "cache", Enum.Parse<SqliteCacheMode>, () => SqliteCacheMode.Default);
        sqliteConnectionBuilder.Pooling = GetOption(customOptions, "pooling", e => e.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase), () => true);

        var connectionString = sqliteConnectionBuilder.ToString();

        // Log SQLite connection parameters
        _logger.LogInformation("SQLite connection string: {ConnectionString}", connectionString);

        Console.WriteLine("=== IMPLEMENTING SQLMITE-STYLE BRIDGE ===");
        var pgConnectionString = $"Host={Environment.GetEnvironmentVariable("JELLYFIN_DB_HOST") ?? "postgres"};Port={Environment.GetEnvironmentVariable("JELLYFIN_DB_PORT") ?? "5432"};Database={Environment.GetEnvironmentVariable("JELLYFIN_DB_NAME") ?? "jellyfin"};Username={Environment.GetEnvironmentVariable("JELLYFIN_DB_USER") ?? "jellyfin"};Password={Environment.GetEnvironmentVariable("JELLYFIN_DB_PASSWORD") ?? "jellyfin"}";
        Console.WriteLine($"=== POSTGRESQL TARGET: {pgConnectionString} ===");

        // Use PostgreSQL directly but let SQLite migrations run
        // This is the core of what SQLMite does - translate SQLite to PostgreSQL
        options.UseNpgsql(pgConnectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly("Jellyfin.Server.Implementations");
            // Enable migration translation
            npgsqlOptions.CommandTimeout(30);
        });

        // Add SQLMite-style command interceptor for DDL translation
        options.AddInterceptors(new SqliteToPostgreSqlTranslator());

        // PATCH: DateTimeColumnInterceptor disabled - SqliteToPostgreSqlTranslator handles DateTime conversion
        // options.AddInterceptors(new DateTimeColumnInterceptor());

        // PATCH: DateTimeReaderInterceptor disabled - causing Npgsql type conversion errors
        // options.AddInterceptors(new DateTimeReaderInterceptor());

        Console.WriteLine("=== SQLMITE-STYLE: POSTGRESQL WITH SQLITE MIGRATION COMPATIBILITY ===");

        var enableSensitiveDataLogging = GetOption(customOptions, "EnableSensitiveDataLogging", e => e.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase), () => false);
        if (enableSensitiveDataLogging)
        {
            options.EnableSensitiveDataLogging(enableSensitiveDataLogging);
            _logger.LogInformation("EnableSensitiveDataLogging is enabled on SQLite connection");
        }
    }

    /// <inheritdoc/>
    public async Task RunScheduledOptimisation(CancellationToken cancellationToken)
    {
        var context = await DbContextFactory!.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            await context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE)", cancellationToken).ConfigureAwait(false);
            await context.Database.ExecuteSqlRawAsync("PRAGMA optimize", cancellationToken).ConfigureAwait(false);
            await context.Database.ExecuteSqlRawAsync("VACUUM", cancellationToken).ConfigureAwait(false);
            await context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE)", cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("jellyfin.db optimized successfully!");
        }
    }

    /// <inheritdoc/>
    public void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.SetDefaultDateTimeKind(DateTimeKind.Utc);
    }

    /// <inheritdoc/>
    public async Task RunShutdownTask(CancellationToken cancellationToken)
    {
        if (DbContextFactory is null)
        {
            return;
        }

        // Run before disposing the application
        var context = await DbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            await context.Database.ExecuteSqlRawAsync("PRAGMA optimize", cancellationToken).ConfigureAwait(false);
        }

        SqliteConnection.ClearAllPools();
    }

    /// <inheritdoc/>
    public void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Conventions.Add(_ => new DoNotUseReturningClauseConvention());
    }

    /// <inheritdoc />
    public Task<string> MigrationBackupFast(CancellationToken cancellationToken)
    {
        var key = DateTime.UtcNow.ToString("yyyyMMddhhmmss", CultureInfo.InvariantCulture);
        var path = Path.Combine(_applicationPaths.DataPath, "jellyfin.db");
        var backupFile = Path.Combine(_applicationPaths.DataPath, BackupFolderName);
        Directory.CreateDirectory(backupFile);

        backupFile = Path.Combine(backupFile, $"{key}_jellyfin.db");
        File.Copy(path, backupFile);
        return Task.FromResult(key);
    }

    /// <inheritdoc />
    public Task RestoreBackupFast(string key, CancellationToken cancellationToken)
    {
        // ensure there are absolutly no dangling Sqlite connections.
        SqliteConnection.ClearAllPools();
        var path = Path.Combine(_applicationPaths.DataPath, "jellyfin.db");
        var backupFile = Path.Combine(_applicationPaths.DataPath, BackupFolderName, $"{key}_jellyfin.db");

        if (!File.Exists(backupFile))
        {
            _logger.LogCritical("Tried to restore a backup that does not exist: {Key}", key);
            return Task.CompletedTask;
        }

        File.Copy(backupFile, path, true);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteBackup(string key)
    {
        var backupFile = Path.Combine(_applicationPaths.DataPath, BackupFolderName, $"{key}_jellyfin.db");

        if (!File.Exists(backupFile))
        {
            _logger.LogCritical("Tried to delete a backup that does not exist: {Key}", key);
            return Task.CompletedTask;
        }

        File.Delete(backupFile);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task PurgeDatabase(JellyfinDbContext dbContext, IEnumerable<string>? tableNames)
    {
        ArgumentNullException.ThrowIfNull(tableNames);

        var deleteQueries = new List<string>();
        foreach (var tableName in tableNames)
        {
            deleteQueries.Add($"DELETE FROM \"{tableName}\";");
        }

        var deleteAllQuery =
        $"""
        PRAGMA foreign_keys = OFF;
        {string.Join('\n', deleteQueries)}
        PRAGMA foreign_keys = ON;
        """;

        await dbContext.Database.ExecuteSqlRawAsync(deleteAllQuery).ConfigureAwait(false);
    }
}
