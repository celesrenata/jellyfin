using System;
using System.Data;
using Npgsql;

namespace Jellyfin.Data
{
    /// <summary>
    /// PostgreSQL database provider implementation.
    /// </summary>
    public sealed class PostgreSQLDatabaseProvider : DatabaseProvider
    {
        private NpgsqlConnection? _connection;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PostgreSQLDatabaseProvider"/> class.
        /// </summary>
        /// <param name="options">The PostgreSQL configuration options.</param>
        public PostgreSQLDatabaseProvider(PostgreSQLConfigurationOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            // Build a standard Npgsql connection string. You can add Opts (SSLMode, Pooling) later.
            var cs = $"Host={options.Host};Port={options.Port};Database={options.Database};Username={options.Username};Password={options.Password}";

            _connection = new NpgsqlConnection(cs);

            // Open immediately so failures surface early.
            _connection.Open();
        }

        /// <summary>
        /// Return the open connection.
        /// </summary>
        /// <returns>The database connection.</returns>
        public override IDbConnection GetConnection()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(_connection);

            return _connection;
        }

        /// <summary>
        /// Dispose managed/unmanaged resources (pattern hook from base).
        /// </summary>
        /// <param name="disposing">True if disposing managed resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _connection?.Dispose();
                _connection = null;
            }

            _disposed = true;

            // no base resources to dispose right now, but call base for future proofing
            base.Dispose(disposing);
        }
    }
}
