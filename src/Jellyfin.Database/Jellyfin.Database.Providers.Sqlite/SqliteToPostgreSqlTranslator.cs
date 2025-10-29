using System;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Jellyfin.Database.Providers.Sqlite
{
    internal class SqliteToPostgreSqlTranslator : DbCommandInterceptor
    {
        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            TranslateCommand(command);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            TranslateCommand(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result)
        {
            TranslateCommand(command);
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            TranslateCommand(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        private static void TranslateCommand(DbCommand command)
        {
            if (string.IsNullOrEmpty(command.CommandText))
            {
                return;
            }

            var originalSql = command.CommandText;

            // FINAL FIX: Prevent double CAST conversion with better detection
            var dateTimeColumns = new[] { "DateCreated", "DateLastMediaAdded", "DateLastRefreshed", "DateLastSaved", "DateModified", "EndDate", "PremiereDate", "StartDate" };
            bool patched = false;

            foreach (var column in dateTimeColumns)
            {
                // Skip if already converted in any form
                if (originalSql.Contains($"CAST(\"{column}\"", StringComparison.Ordinal) ||
                    originalSql.Contains($"CAST({column}.", StringComparison.Ordinal))
                {
                    continue;
                }

                // Handle table-qualified columns: table."DateCreated" -> CAST(table."DateCreated" AS TIMESTAMP)
                var qualifiedPattern = @"(\w+\.)" + "\"" + column + "\"";
                var qualifiedReplacement = @"CAST($1""" + column + @""" AS TIMESTAMP)";

                if (System.Text.RegularExpressions.Regex.IsMatch(originalSql, qualifiedPattern))
                {
                    originalSql = System.Text.RegularExpressions.Regex.Replace(originalSql, qualifiedPattern, qualifiedReplacement);
                    patched = true;
                }

                // Handle unqualified columns: "DateCreated" -> CAST("DateCreated" AS TIMESTAMP)
                // But only if not already qualified
                var unqualifiedPattern = @"(?<!\w\.)" + "\"" + column + "\"";
                var unqualifiedReplacement = @"CAST(""" + column + @""" AS TIMESTAMP)";

                if (System.Text.RegularExpressions.Regex.IsMatch(originalSql, unqualifiedPattern))
                {
                    originalSql = System.Text.RegularExpressions.Regex.Replace(originalSql, unqualifiedPattern, unqualifiedReplacement);
                    patched = true;
                }
            }

            if (patched)
            {
                command.CommandText = originalSql;
                return;
            }

            var translatedSql = TranslateSqliteToPostgreSql(originalSql);

            if (translatedSql != originalSql)
            {
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
                command.CommandText = translatedSql;
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
            }
        }

        private static string TranslateSqliteToPostgreSql(string sqliteQuery)
        {
            var query = sqliteQuery;

            // Handle boolean comparisons - SQLite uses integers (0/1) for booleans
            // More comprehensive boolean handling with type casting
            query = System.Text.RegularExpressions.Regex.Replace(query, @"(\w+)\s*=\s*0\b", @"(CAST($1 AS INTEGER) = 0)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            query = System.Text.RegularExpressions.Regex.Replace(query, @"(\w+)\s*=\s*1\b", @"(CAST($1 AS INTEGER) = 1)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            query = System.Text.RegularExpressions.Regex.Replace(query, @"(\w+)\s*!=\s*0\b", @"(CAST($1 AS INTEGER) != 0)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            query = System.Text.RegularExpressions.Regex.Replace(query, @"(\w+)\s*!=\s*1\b", @"(CAST($1 AS INTEGER) != 1)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Handle qualified column names (table.column = 0/1)
            query = System.Text.RegularExpressions.Regex.Replace(query, @"(\w+\.\w+)\s*=\s*0\b", @"(CAST($1 AS INTEGER) = 0)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            query = System.Text.RegularExpressions.Regex.Replace(query, @"(\w+\.\w+)\s*=\s*1\b", @"(CAST($1 AS INTEGER) = 1)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            query = System.Text.RegularExpressions.Regex.Replace(query, @"(\w+\.\w+)\s*!=\s*0\b", @"(CAST($1 AS INTEGER) != 0)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            query = System.Text.RegularExpressions.Regex.Replace(query, @"(\w+\.\w+)\s*!=\s*1\b", @"(CAST($1 AS INTEGER) != 1)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Handle quoted column names ("column" = 0/1)
            query = System.Text.RegularExpressions.Regex.Replace(query, @"(""[^""]+"")\s*=\s*0\b", @"(CAST($1 AS INTEGER) = 0)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            query = System.Text.RegularExpressions.Regex.Replace(query, @"(""[^""]+"")\s*=\s*1\b", @"(CAST($1 AS INTEGER) = 1)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            query = System.Text.RegularExpressions.Regex.Replace(query, @"(""[^""]+"")\s*!=\s*0\b", @"(CAST($1 AS INTEGER) != 0)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            query = System.Text.RegularExpressions.Regex.Replace(query, @"(""[^""]+"")\s*!=\s*1\b", @"(CAST($1 AS INTEGER) != 1)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Handle UUID columns - cast text values to UUID for ExternalId and other ID columns
            // Simple pattern for any ExternalId parameter assignment
            query = System.Text.RegularExpressions.Regex.Replace(query, @"""ExternalId""\s*=\s*(@p\d+)", @"""ExternalId"" = CAST($1 AS UUID)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            query = System.Text.RegularExpressions.Regex.Replace(query, @"ExternalId\s*=\s*(@p\d+)", @"ExternalId = CAST($1 AS UUID)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            query = System.Text.RegularExpressions.Regex.Replace(query, @"""ExternalSeriesId""\s*=\s*(@p\d+)", @"""ExternalSeriesId"" = CAST($1 AS UUID)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            query = System.Text.RegularExpressions.Regex.Replace(query, @"ExternalSeriesId\s*=\s*(@p\d+)", @"ExternalSeriesId = CAST($1 AS UUID)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            query = System.Text.RegularExpressions.Regex.Replace(query, @"""ExternalServiceId""\s*=\s*(@p\d+)", @"""ExternalServiceId"" = CAST($1 AS UUID)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            query = System.Text.RegularExpressions.Regex.Replace(query, @"ExternalServiceId\s*=\s*(@p\d+)", @"ExternalServiceId = CAST($1 AS UUID)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Handle INSERT VALUES with ExternalId parameters - find ExternalId position and cast corresponding parameter
            if (query.Contains("INSERT INTO \"BaseItems\"", StringComparison.OrdinalIgnoreCase) && query.Contains("ExternalId", StringComparison.OrdinalIgnoreCase))
            {
                var columnsMatch = System.Text.RegularExpressions.Regex.Match(query, @"INSERT INTO ""BaseItems"" \(([^)]+)\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var valuesMatch = System.Text.RegularExpressions.Regex.Match(query, @"VALUES \(([^)]+)\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (columnsMatch.Success && valuesMatch.Success)
                {
                    var columns = columnsMatch.Groups[1].Value.Split(',').Select(c => c.Trim().Trim('"')).ToArray();
                    var values = valuesMatch.Groups[1].Value.Split(',').Select(v => v.Trim()).ToArray();
                    bool modified = false;

                    for (int i = 0; i < columns.Length && i < values.Length; i++)
                    {
                        if (columns[i].Equals("ExternalId", StringComparison.OrdinalIgnoreCase) ||
                            columns[i].Equals("ExternalSeriesId", StringComparison.OrdinalIgnoreCase) ||
                            columns[i].Equals("ExternalServiceId", StringComparison.OrdinalIgnoreCase) ||
                            columns[i].Equals("OwnerId", StringComparison.OrdinalIgnoreCase) ||
                            columns[i].Equals("PrimaryVersionId", StringComparison.OrdinalIgnoreCase) ||
                            columns[i].Equals("ShowId", StringComparison.OrdinalIgnoreCase))
                        {
                            if (values[i].StartsWith("@p", StringComparison.OrdinalIgnoreCase))
                            {
                                values[i] = $"CAST({values[i]} AS UUID)";
                                modified = true;
                            }
                        }
                    }

                    if (modified)
                    {
                        query = System.Text.RegularExpressions.Regex.Replace(query, @"VALUES \([^)]+\)", $"VALUES ({string.Join(", ", values)})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    }
                }
            }

            // Convert AUTOINCREMENT to SERIAL
            query = System.Text.RegularExpressions.Regex.Replace(query, @"INTEGER\s+PRIMARY\s+KEY\s+AUTOINCREMENT", "SERIAL PRIMARY KEY", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            query = System.Text.RegularExpressions.Regex.Replace(query, @"INTEGER\s+AUTOINCREMENT", "SERIAL", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Convert TEXT to VARCHAR
            query = System.Text.RegularExpressions.Regex.Replace(query, @"\bTEXT\b", "VARCHAR(65535)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Convert REAL to DOUBLE PRECISION
            query = System.Text.RegularExpressions.Regex.Replace(query, @"\bREAL\b", "DOUBLE PRECISION", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Remove COLLATE NOCASE
            query = System.Text.RegularExpressions.Regex.Replace(query, @"\s+COLLATE\s+NOCASE", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Convert datetime('now') to NOW()
            query = System.Text.RegularExpressions.Regex.Replace(query, @"datetime\s*\(\s*'now'\s*\)", "NOW()", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Convert SQLite PRAGMA to PostgreSQL equivalent (use SELECT 1 as no-op)
            query = System.Text.RegularExpressions.Regex.Replace(query, @"PRAGMA\s+.*", "SELECT 1", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Convert IF NOT EXISTS for CREATE TABLE
            query = System.Text.RegularExpressions.Regex.Replace(query, @"CREATE\s+TABLE\s+IF\s+NOT\s+EXISTS", "CREATE TABLE IF NOT EXISTS", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return query;
        }
    }
}
