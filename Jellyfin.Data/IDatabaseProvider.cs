using System;
using System.Data;

namespace Jellyfin.Data
{
    /// <summary>
    /// Interface for database providers.
    /// </summary>
    public interface IDatabaseProvider : IDisposable
    {
        /// <summary>
        /// Gets a database connection.
        /// </summary>
        /// <returns>A database connection.</returns>
        IDbConnection GetConnection();
    }
}
