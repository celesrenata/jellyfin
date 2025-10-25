using System;

namespace Jellyfin.Data
{
    /// <summary>
    /// Configuration options for the PostgreSQL database.
    /// </summary>
    public class PostgreSQLConfigurationOptions : DatabaseConfigurationOptions
    {
        // Properties inherited from DatabaseConfigurationOptions
        // No need to redefine Host, Port, etc. as they are already in base class
    }
}
