using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.DbConfiguration;
using Jellyfin.Database.Implementations.Locking;
using Jellyfin.Database.Providers.Sqlite;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using JellyfinDbProviderFactory = System.Func<System.IServiceProvider, Jellyfin.Database.Implementations.IJellyfinDatabaseProvider>;

namespace Jellyfin.Server.Implementations.Extensions;

/// <summary>
/// Extensions for the <see cref="IServiceCollection"/> interface.
/// </summary>
public static class ServiceCollectionExtensions
{
    static ServiceCollectionExtensions()
    {
        Console.WriteLine("=== ServiceCollectionExtensions STATIC CONSTRUCTOR CALLED ===");
    }

    private static IEnumerable<Type> DatabaseProviderTypes()
    {
        Console.WriteLine("=== DatabaseProviderTypes CALLED - FORCING POSTGRESQL ONLY ===");
        // FORCE POSTGRESQL - USE HIJACKED SQLITE PROVIDER
        yield return typeof(Jellyfin.Database.Providers.Sqlite.SqliteDatabaseProvider);
    }

    private static IDictionary<string, JellyfinDbProviderFactory> GetSupportedDbProviders()
    {
        var items = new Dictionary<string, JellyfinDbProviderFactory>(StringComparer.InvariantCultureIgnoreCase);
        foreach (var providerType in DatabaseProviderTypes())
        {
            var keyAttribute = providerType.GetCustomAttribute<JellyfinDatabaseProviderKeyAttribute>();
            if (keyAttribute is null || string.IsNullOrWhiteSpace(keyAttribute.DatabaseProviderKey))
            {
                continue;
            }

            var provider = providerType;
            items[keyAttribute.DatabaseProviderKey] = (services) => (IJellyfinDatabaseProvider)ActivatorUtilities.CreateInstance(services, providerType);
        }

        return items;
    }

    private static JellyfinDbProviderFactory? LoadDatabasePlugin(CustomDatabaseOptions customProviderOptions, IApplicationPaths applicationPaths)
    {
        var plugin = Directory.EnumerateDirectories(applicationPaths.PluginsPath)
            .Where(e => Path.GetFileName(e)!.StartsWith(customProviderOptions.PluginName, StringComparison.OrdinalIgnoreCase))
            .Order()
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"The requested custom database plugin with the name '{customProviderOptions.PluginName}' could not been found in '{applicationPaths.PluginsPath}'");

        var dbProviderAssembly = Path.Combine(plugin, Path.ChangeExtension(customProviderOptions.PluginAssembly, "dll"));
        if (!File.Exists(dbProviderAssembly))
        {
            throw new InvalidOperationException($"Could not find the requested assembly at '{dbProviderAssembly}'");
        }

        // we have to load the assembly without proxy to ensure maximum performance for this.
        var assembly = Assembly.LoadFrom(dbProviderAssembly);
        var dbProviderType = assembly.GetExportedTypes().FirstOrDefault(f => f.IsAssignableTo(typeof(IJellyfinDatabaseProvider)))
            ?? throw new InvalidOperationException($"Could not find any type implementing the '{nameof(IJellyfinDatabaseProvider)}' interface.");

        return (services) => (IJellyfinDatabaseProvider)ActivatorUtilities.CreateInstance(services, dbProviderType);
    }

    /// <summary>
    /// Adds the <see cref="IDbContextFactory{TContext}"/> interface to the service collection with second level caching enabled.
    /// </summary>
    /// <param name="serviceCollection">An instance of the <see cref="IServiceCollection"/> interface.</param>
    /// <param name="configurationManager">The server configuration manager.</param>
    /// <param name="configuration">The startup Configuration.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddJellyfinDbContext(
        this IServiceCollection serviceCollection,
        IServerConfigurationManager configurationManager,
        IConfiguration configuration)
    {
        Console.WriteLine("=== FORCING POSTGRESQL NO MATTER WHAT ===");

        // Force PostgreSQL configuration
        var efCoreConfiguration = new Jellyfin.Database.Implementations.DbConfiguration.DatabaseConfigurationOptions
        {
            DatabaseType = "Jellyfin-PostgreSQL",
            LockingBehavior = DatabaseLockingBehaviorTypes.NoLock
        };
        configurationManager.SaveConfiguration("database", efCoreConfiguration);

        JellyfinDbProviderFactory? providerFactory = null;
        var providers = GetSupportedDbProviders();
        Console.WriteLine($"=== AVAILABLE PROVIDERS: {string.Join(", ", providers.Keys)} ===");

        // Force PostgreSQL provider - try different case variations
        if (!providers.TryGetValue("JELLYFIN-POSTGRESQL", out providerFactory!) &&
            !providers.TryGetValue("Jellyfin-PostgreSQL", out providerFactory!) &&
            !providers.TryGetValue("jellyfin-postgresql", out providerFactory!))
        {
            Console.WriteLine("=== POSTGRESQL PROVIDER NOT FOUND - USING FIRST AVAILABLE ===");
            providerFactory = providers.Values.First();
        }

        Console.WriteLine("=== POSTGRESQL PROVIDER SELECTED ===");

        // Add PostgreSQL configuration options to DI
        serviceCollection.AddSingleton(new Jellyfin.Data.PostgreSQLConfigurationOptions
        {
            Host = Environment.GetEnvironmentVariable("JELLYFIN_DB_HOST") ?? "postgres",
            Port = int.Parse(Environment.GetEnvironmentVariable("JELLYFIN_DB_PORT") ?? "5432", CultureInfo.InvariantCulture),
            Database = Environment.GetEnvironmentVariable("JELLYFIN_DB_NAME") ?? "jellyfin",
            Username = Environment.GetEnvironmentVariable("JELLYFIN_DB_USER") ?? "jellyfin",
            Password = Environment.GetEnvironmentVariable("JELLYFIN_DB_PASSWORD") ?? "jellyfin"
        });

        serviceCollection.AddSingleton<IJellyfinDatabaseProvider>(providerFactory!);

        switch (efCoreConfiguration.LockingBehavior)
        {
            case DatabaseLockingBehaviorTypes.NoLock:
                serviceCollection.AddSingleton<IEntityFrameworkCoreLockingBehavior, NoLockBehavior>();
                break;
            case DatabaseLockingBehaviorTypes.Pessimistic:
                serviceCollection.AddSingleton<IEntityFrameworkCoreLockingBehavior, PessimisticLockBehavior>();
                break;
            case DatabaseLockingBehaviorTypes.Optimistic:
                serviceCollection.AddSingleton<IEntityFrameworkCoreLockingBehavior, OptimisticLockBehavior>();
                break;
        }

        serviceCollection.AddPooledDbContextFactory<JellyfinDbContext>((serviceProvider, opt) =>
        {
            var provider = serviceProvider.GetRequiredService<IJellyfinDatabaseProvider>();
            provider.Initialise(opt, efCoreConfiguration);
            var lockingBehavior = serviceProvider.GetRequiredService<IEntityFrameworkCoreLockingBehavior>();
            lockingBehavior.Initialise(opt);
        });

        return serviceCollection;
    }
}
