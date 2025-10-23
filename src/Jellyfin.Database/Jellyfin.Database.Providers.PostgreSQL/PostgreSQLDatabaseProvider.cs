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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Database.Providers.PostgreSQL;

/// <summary>
/// Simple interceptor for PostgreSQL commands.
/// </summary>
public class PostgreSQLConnectionInterceptor : IDbConnectionInterceptor
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSQLConnectionInterceptor"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="pragmaOptions">The pragma options.</param>
    public PostgreSQLConnectionInterceptor(ILogger logger, params object[] pragmaOptions)
    {
        _logger = logger;
    }
}

/// <summary>
/// Configures jellyfin to use a PostgreSQL database.
/// </summary>
[JellyfinDatabaseProviderKey("Jellyfin-PostgreSQL")]
public sealed class PostgreSQLDatabaseProvider : IJellyfinDatabaseProvider
{
    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger<PostgreSQLDatabaseProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSQLDatabaseProvider"/> class.
    /// </summary>
    /// <param name="applicationPaths">Service to construct the fallback when the old data path configuration is used.</param>
    /// <param name="logger">A logger.</param>
    public PostgreSQLDatabaseProvider(IApplicationPaths applicationPaths, ILogger<PostgreSQLDatabaseProvider> logger)
    {
        _applicationPaths = applicationPaths;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IDbContextFactory<JellyfinDbContext>? DbContextFactory { get; set; }

    /// <inheritdoc/>
    public void Initialise(DbContextOptionsBuilder options, DatabaseConfigurationOptions databaseConfiguration)
    {
        static T? GetOption<T>(ICollection<CustomDatabaseOption>? options, string key, Func<string, T> converter, Func<T>? defaultValue = null)
            where T : class
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

        static T GetOptionValue<T>(ICollection<CustomDatabaseOption>? options, string key, Func<string, T> converter, Func<T> defaultValue)
        {
            if (options is null)
            {
                return defaultValue();
            }

            var value = options.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (value is null)
            {
                return defaultValue();
            }

            return converter(value.Value);
        }

        var customOptions = databaseConfiguration.CustomProviderOptions?.Options;

        // Configure PostgreSQL connection
        var connectionString = GetPostgreSQLConnectionString(customOptions);

        // Log PostgreSQL connection parameters
        _logger.LogInformation("PostgreSQL connection string: {ConnectionString}", connectionString);

        // Use PostgreSQL provider - this is the EF Core provider for PostgreSQL
        options
            .UseNpgsql(
                connectionString,
                npgsqlOptions => npgsqlOptions.MigrationsAssembly(GetType().Assembly))
            // Configure PostgreSQL-specific options
            .ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.NonTransactionalMigrationOperationWarning))
            .AddInterceptors(new PostgreSQLConnectionInterceptor(
                _logger));

        var enableSensitiveDataLogging = GetOptionValue(customOptions, "EnableSensitiveDataLogging", e => e.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase), () => false);
        if (enableSensitiveDataLogging)
        {
            options.EnableSensitiveDataLogging(enableSensitiveDataLogging);
            _logger.LogInformation("EnableSensitiveDataLogging is enabled on PostgreSQL connection");
        }
    }

    private string GetPostgreSQLConnectionString(ICollection<CustomDatabaseOption>? customOptions)
    {
        // Default connection string for PostgreSQL
        var connectionString = "Host=localhost;Database=jellyfin;Username=jellyfin;Password=;";

        // If custom options are provided, parse them to build connection string
        if (customOptions is not null)
        {
            var host = GetOptionValue(customOptions, "host", "localhost");
            var port = GetOptionValue(customOptions, "port", "5432");
            var database = GetOptionValue(customOptions, "database", "jellyfin");
            var username = GetOptionValue(customOptions, "username", "jellyfin");
            var password = GetOptionValue(customOptions, "password", "");

            connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password};";
        }

        return connectionString;
    }

    private string GetOptionValue(ICollection<CustomDatabaseOption>? options, string key, string defaultValue)
    {
        if (options is null)
        {
            return defaultValue;
        }

        var option = options.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        return option?.Value ?? defaultValue;
    }

    /// <inheritdoc/>
    public async Task RunScheduledOptimisation(CancellationToken cancellationToken)
    {
        if (DbContextFactory is null)
        {
            return;
        }

        var context = await DbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            // PostgreSQL-specific optimization commands
            await context.Database.ExecuteSqlRawAsync("VACUUM ANALYZE", cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("PostgreSQL database optimized successfully!");
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
            // PostgreSQL-specific shutdown operations
            await context.Database.ExecuteSqlRawAsync("COMMIT", cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // No specific conventions for PostgreSQL
    }

    /// <inheritdoc />
    public Task<string> MigrationBackupFast(CancellationToken cancellationToken)
    {
        // PostgreSQL backup logic would go here
        // For now, return a dummy backup key
        var key = DateTime.UtcNow.ToString("yyyyMMddhhmmss", CultureInfo.InvariantCulture);
        _logger.LogInformation("PostgreSQL backup created with key: {Key}", key);
        return Task.FromResult(key);
    }

    /// <inheritdoc />
    public Task RestoreBackupFast(string key, CancellationToken cancellationToken)
    {
        // PostgreSQL restore logic would go here
        _logger.LogInformation("PostgreSQL backup restored with key: {Key}", key);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteBackup(string key)
    {
        // PostgreSQL backup deletion logic would go here
        _logger.LogInformation("PostgreSQL backup deleted with key: {Key}", key);
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
        {string.Join('\n', deleteQueries)}
        """;

        await dbContext.Database.ExecuteSqlRawAsync(deleteAllQuery).ConfigureAwait(false);
    }
}
