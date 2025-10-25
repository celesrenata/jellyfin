using Jellyfin.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Server.Implementations
{
    /// <summary>
    /// Registry for database providers.
    /// </summary>
    public static class DatabaseProviderRegistry
    {
        /// <summary>
        /// Registers database providers with the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        public static void RegisterDatabaseProviders(IServiceCollection services)
        {
            services.AddSingleton<IDatabaseProvider, PostgreSQLDatabaseProvider>();
        }
    }
}
