using System;
using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Data
{
    /// <summary>
    /// Configuration options for the database.
    /// </summary>
    public class DatabaseConfigurationOptions
    {
        /// <summary>
        /// Gets or sets the database host.
        /// </summary>
        public required string Host { get; set; }

        /// <summary>
        /// Gets or sets the database port.
        /// </summary>
        public required int Port { get; set; }

        /// <summary>
        /// Gets or sets the database name.
        /// </summary>
        public required string Database { get; set; }

        /// <summary>
        /// Gets or sets the database username.
        /// </summary>
        public required string Username { get; set; }

        /// <summary>
        /// Gets or sets the database password.
        /// </summary>
        public required string Password { get; set; }
    }
}
