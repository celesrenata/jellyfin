using System;
using System.Data;

namespace Jellyfin.Data
{
    /// <summary>
    /// Base class for database providers.
    /// </summary>
    public abstract class DatabaseProvider : IDatabaseProvider
    {
        /// <summary>
        /// Gets a database connection.
        /// </summary>
        /// <returns>A database connection.</returns>
        public abstract IDbConnection GetConnection();

        /// <summary>
        /// Disposes the database provider.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the database provider resources.
        /// </summary>
        /// <param name="disposing">True if called from Dispose, false if called from finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            // Base class disposal logic
        }
    }
}
