using System;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Jellyfin.Database.Providers.Sqlite
{
    internal class DateTimeReaderInterceptor : DbCommandInterceptor
    {
        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            return base.ReaderExecuting(command, eventData, result);
        }

        public override DbDataReader ReaderExecuted(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result)
        {
            // Wrap the reader to handle VARCHAR to DateTime conversion
            if (command.CommandText.Contains("BaseItems", StringComparison.OrdinalIgnoreCase))
            {
                return new DateTimeConvertingReader(result);
            }

            return base.ReaderExecuted(command, eventData, result);
        }
    }
}
