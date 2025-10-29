using System;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Jellyfin.Database.Providers.Sqlite
{
    internal class DateTimeColumnInterceptor : DbCommandInterceptor
    {
        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            PatchDateTimeColumns(command);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result)
        {
            PatchDateTimeColumns(command);
            return base.NonQueryExecuting(command, eventData, result);
        }

        private static void PatchDateTimeColumns(DbCommand command)
        {
            if (string.IsNullOrEmpty(command.CommandText))
            {
                return;
            }

            // Only patch BaseItems queries that need DateTime conversion
            if (!command.CommandText.Contains("BaseItems", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var dateTimeColumns = new[] { "DateCreated", "DateLastMediaAdded", "DateLastRefreshed", "DateLastSaved", "DateModified", "EndDate", "PremiereDate", "StartDate" };
            bool patched = false;

            foreach (var column in dateTimeColumns)
            {
                var originalColumn = $"\"{column}\"";
                var convertedColumn = $"\"{column}\"::timestamp";

                // Only replace if not already converted and not already in a CAST expression
                // Check for various CAST patterns: CAST("DateCreated", d.CAST("DateCreated", etc.
                if (command.CommandText.Contains(originalColumn, StringComparison.OrdinalIgnoreCase) &&
                    !command.CommandText.Contains(convertedColumn, StringComparison.OrdinalIgnoreCase) &&
                    !command.CommandText.Contains($"CAST({originalColumn}", StringComparison.OrdinalIgnoreCase) &&
                    !command.CommandText.Contains($".CAST({originalColumn}", StringComparison.OrdinalIgnoreCase))
                {
                    command.CommandText = command.CommandText.Replace(originalColumn, convertedColumn, StringComparison.OrdinalIgnoreCase);
                    patched = true;
                }
            }

            if (patched)
            {
                Console.WriteLine("=== DATETIME INTERCEPTOR: ADDED TIMESTAMP CONVERSION ===");
            }
        }
    }
}
