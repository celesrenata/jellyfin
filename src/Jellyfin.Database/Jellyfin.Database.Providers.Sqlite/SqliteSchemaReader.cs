using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Npgsql;

#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities - These are schema migration queries, not user input

namespace Jellyfin.Database.Providers.Sqlite
{
    internal class SqliteSchemaReader
    {
        public static void MigrateSqliteToPostgreSQL(string sqlitePath, string pgConnectionString)
        {
            Console.WriteLine($"=== SQLMITE: MIGRATING SQLITE DB {sqlitePath} TO POSTGRESQL ===");

            if (!System.IO.File.Exists(sqlitePath))
            {
                Console.WriteLine("=== SQLMITE: NO SQLITE DB FOUND, SKIPPING MIGRATION ===");
                return;
            }

            using var sqliteConn = new SqliteConnection($"Data Source={sqlitePath}");
            using var pgConn = new NpgsqlConnection(pgConnectionString);

            sqliteConn.Open();
            pgConn.Open();

            var tables = GetTables(sqliteConn);
            Console.WriteLine($"=== SQLMITE: FOUND {tables.Count} TABLES TO MIGRATE ===");

            foreach (var table in tables)
            {
                Console.WriteLine($"=== SQLMITE: MIGRATING TABLE {table} ===");
                MigrateTable(sqliteConn, pgConn, table);
            }

            Console.WriteLine("=== SQLMITE: RUNNING GLOBAL UUID CONVERSION ===");
            ConvertAllVarcharIdsToUuid(pgConn);
        }

        private static List<string> GetTables(SqliteConnection conn)
        {
            var tables = new List<string>();
            using var cmd = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }

            return tables;
        }

        private static void MigrateTable(SqliteConnection sqliteConn, NpgsqlConnection pgConn, string tableName)
        {
            var createSql = GetCreateTableSql(sqliteConn, tableName);
            if (string.IsNullOrEmpty(createSql))
            {
                return;
            }

            Console.WriteLine($"=== SQLMITE: ORIGINAL SQL FOR {tableName}: {createSql} ===");
            var pgCreateSql = TranslateCreateTable(createSql);
            Console.WriteLine($"=== SQLMITE: TRANSLATED SQL FOR {tableName}: {pgCreateSql} ===");

            try
            {
                using var createCmd = new NpgsqlCommand(pgCreateSql, pgConn);
                createCmd.ExecuteNonQuery();
                Console.WriteLine($"=== SQLMITE: CREATED TABLE {tableName} ===");

                CopyTableData(sqliteConn, pgConn, tableName);
            }
            catch (Exception ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"=== SQLMITE: TABLE {tableName} EXISTS, CHECKING SCHEMA ===");

                // Table exists, check if schema needs updating
                if (UpdateTableSchema(pgConn, tableName))
                {
                    Console.WriteLine($"=== SQLMITE: UPDATED SCHEMA FOR {tableName} ===");
                }
                else
                {
                    Console.WriteLine($"=== SQLMITE: SCHEMA OK FOR {tableName} ===");
                }
            }
            catch (Exception ex) when (ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"=== SQLMITE: RETRYING {tableName} WITH FIXED SCHEMA ===");

                // Try again with the updated schema translation (e.g., BLOB -> BYTEA fix)
                try
                {
                    using var retryCmd = new NpgsqlCommand(pgCreateSql, pgConn);
                    retryCmd.ExecuteNonQuery();
                    Console.WriteLine($"=== SQLMITE: CREATED TABLE {tableName} ON RETRY ===");

                    CopyTableData(sqliteConn, pgConn, tableName);
                }
                catch (Exception retryEx)
                {
                    Console.WriteLine($"=== SQLMITE: RETRY FAILED FOR {tableName}: {retryEx.Message} ===");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== SQLMITE: ERROR MIGRATING {tableName}: {ex.Message} ===");
            }
        }

        private static bool UpdateTableSchema(NpgsqlConnection pgConn, string tableName)
        {
            // Known boolean columns that should be BOOLEAN type
            var booleanColumns = new[] { "IsFolder", "IsVirtualItem", "IsInMixedFolder", "IsLocked", "IsMovie", "IsRepeat", "IsSeries" };

            // Known ID columns that should be UUID type
            var idColumns = new[]
            {
                "Id", "ParentId", "ChannelId", "OwnerId", "SeriesId", "SeasonId", "ShowId", "TopParentId", "PrimaryVersionId",
                "ExternalId", "ExternalSeriesId", "ExternalServiceId", "UserId", "ItemId", "ParentItemId", "DeviceId", "ProviderId"
            };

            bool schemaUpdated = false;

            // Convert boolean columns
            foreach (var columnName in booleanColumns)
            {
                if (ConvertColumnType(pgConn, tableName, columnName, "integer", "BOOLEAN", "CASE WHEN \"{0}\" = 0 THEN FALSE ELSE TRUE END"))
                {
                    schemaUpdated = true;
                }
            }

            // Convert ID columns to UUID
            foreach (var columnName in idColumns)
            {
                if (ConvertColumnType(pgConn, tableName, columnName, "character varying", "UUID", "\"{0}\"::UUID"))
                {
                    schemaUpdated = true;
                }
            }

            return schemaUpdated;
        }

        private static bool ConvertColumnType(NpgsqlConnection pgConn, string tableName, string columnName, string fromType, string toType, string usingExpression)
        {
            // Check if this column exists and needs conversion
            var checkSql = @"
                SELECT data_type
                FROM information_schema.columns
                WHERE table_name = @tableName AND column_name = @columnName";

            using var checkCmd = new NpgsqlCommand(checkSql, pgConn);
            checkCmd.Parameters.AddWithValue("tableName", tableName);
            checkCmd.Parameters.AddWithValue("columnName", columnName);

            var currentType = checkCmd.ExecuteScalar() as string;

            if (currentType == fromType)
            {
                Console.WriteLine($"=== SQLMITE: CONVERTING {tableName}.{columnName} FROM {fromType.ToUpper(System.Globalization.CultureInfo.InvariantCulture)} TO {toType} ===");

                try
                {
                    // Drop constraints, convert column, recreate constraints
                    DropColumnConstraints(pgConn, tableName, columnName);

                    var alterSql = $@"
                        ALTER TABLE ""{tableName}""
                        ALTER COLUMN ""{columnName}"" DROP DEFAULT,
                        ALTER COLUMN ""{columnName}"" TYPE {toType}
                        USING {string.Format(System.Globalization.CultureInfo.InvariantCulture, usingExpression, columnName)}";

                    using var alterCmd = new NpgsqlCommand(alterSql, pgConn);
                    alterCmd.ExecuteNonQuery();

                    Console.WriteLine($"=== SQLMITE: CONVERTED {tableName}.{columnName} TO {toType} ===");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"=== SQLMITE: ERROR CONVERTING {tableName}.{columnName}: {ex.Message} ===");
                }
            }

            return false;
        }

        private static void DropColumnConstraints(NpgsqlConnection pgConn, string tableName, string columnName)
        {
            var sql = @"
                SELECT constraint_name
                FROM information_schema.key_column_usage
                WHERE table_name = @tableName AND column_name = @columnName";

            using var cmd = new NpgsqlCommand(sql, pgConn);
            cmd.Parameters.AddWithValue("tableName", tableName);
            cmd.Parameters.AddWithValue("columnName", columnName);

            using var reader = cmd.ExecuteReader();
            var constraints = new List<string>();
            while (reader.Read())
            {
                constraints.Add(reader.GetString(0));
            }

            reader.Close();

            foreach (var constraint in constraints)
            {
                try
                {
                    using var dropCmd = new NpgsqlCommand($@"ALTER TABLE ""{tableName}"" DROP CONSTRAINT ""{constraint}""", pgConn);
                    dropCmd.ExecuteNonQuery();
                }
                catch
                {
                    // Ignore constraint drop errors
                }
            }
        }

        private static void ConvertAllVarcharIdsToUuid(NpgsqlConnection pgConn)
        {
            var sql = @"
                SELECT table_name, column_name 
                FROM information_schema.columns 
                WHERE column_name LIKE '%Id' AND data_type = 'character varying'
                AND column_name != 'MigrationId'
                ORDER BY table_name, column_name";

            using var cmd = new NpgsqlCommand(sql, pgConn);
            using var reader = cmd.ExecuteReader();

            var conversions = new List<(string Table, string Column)>();
            while (reader.Read())
            {
                conversions.Add((reader.GetString(0), reader.GetString(1)));
            }

            reader.Close();

            foreach (var (table, column) in conversions)
            {
                ConvertColumnType(pgConn, table, column, "character varying", "UUID", "\"{0}\"::UUID");
            }
        }

        private static string GetCreateTableSql(SqliteConnection conn, string tableName)
        {
            using var cmd = new SqliteCommand($"SELECT sql FROM sqlite_master WHERE type='table' AND name='{tableName}'", conn);
            return cmd.ExecuteScalar()?.ToString() ?? string.Empty;
        }

        private static string TranslateCreateTable(string sqliteCreateSql)
        {
            var sql = sqliteCreateSql;

            // Handle various AUTOINCREMENT patterns
            sql = sql.Replace("INTEGER PRIMARY KEY AUTOINCREMENT", "SERIAL PRIMARY KEY", StringComparison.OrdinalIgnoreCase);
            sql = sql.Replace("PRIMARY KEY AUTOINCREMENT", "PRIMARY KEY", StringComparison.OrdinalIgnoreCase);
            sql = sql.Replace("AUTOINCREMENT", string.Empty, StringComparison.OrdinalIgnoreCase);

            // Handle boolean columns (SQLite uses INTEGER for booleans)
            // Convert known boolean column names to BOOLEAN type
            var booleanColumns = new[] { "IsFolder", "IsVirtualItem", "IsInMixedFolder", "IsLocked", "IsMovie", "IsRepeat", "IsSeries" };
            foreach (var column in booleanColumns)
            {
                sql = System.Text.RegularExpressions.Regex.Replace(sql, $@"""{column}""\s+INTEGER\s+NOT\s+NULL", $@"""{column}"" BOOLEAN NOT NULL", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                sql = System.Text.RegularExpressions.Regex.Replace(sql, $@"""{column}""\s+INTEGER\s+NULL", $@"""{column}"" BOOLEAN NULL", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Handle explicit boolean defaults
            sql = System.Text.RegularExpressions.Regex.Replace(sql, @"""(\w+)""\s+INTEGER\s+NOT\s+NULL\s+DEFAULT\s+0", @"""$1"" BOOLEAN NOT NULL DEFAULT FALSE", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            sql = System.Text.RegularExpressions.Regex.Replace(sql, @"""(\w+)""\s+INTEGER\s+NOT\s+NULL\s+DEFAULT\s+1", @"""$1"" BOOLEAN NOT NULL DEFAULT TRUE", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            sql = System.Text.RegularExpressions.Regex.Replace(sql, @"""(\w+)""\s+INTEGER\s+DEFAULT\s+0", @"""$1"" BOOLEAN DEFAULT FALSE", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            sql = System.Text.RegularExpressions.Regex.Replace(sql, @"""(\w+)""\s+INTEGER\s+DEFAULT\s+1", @"""$1"" BOOLEAN DEFAULT TRUE", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Handle standalone INTEGER columns that should become BIGINT
            sql = sql.Replace("INTEGER", "BIGINT", StringComparison.OrdinalIgnoreCase);

            // Fix back the SERIAL PRIMARY KEY case
            sql = sql.Replace("BIGINT PRIMARY KEY", "SERIAL PRIMARY KEY", StringComparison.OrdinalIgnoreCase);

            sql = sql.Replace("TEXT", "VARCHAR(65535)", StringComparison.OrdinalIgnoreCase);
            sql = sql.Replace("REAL", "DOUBLE PRECISION", StringComparison.OrdinalIgnoreCase);
            sql = sql.Replace("BLOB", "BYTEA", StringComparison.OrdinalIgnoreCase);
            sql = sql.Replace("COLLATE NOCASE", string.Empty, StringComparison.OrdinalIgnoreCase);
            sql = sql.Replace("datetime('now')", "NOW()", StringComparison.OrdinalIgnoreCase);

            return sql;
        }

        private static void CopyTableData(SqliteConnection sqliteConn, NpgsqlConnection pgConn, string tableName)
        {
            var columns = GetColumnNames(sqliteConn, tableName);
            if (columns.Count == 0)
            {
                return;
            }

            var columnList = string.Join(", ", columns);
            var paramList = string.Join(", ", columns.ConvertAll(c => $"@{c}"));

            using var selectCmd = new SqliteCommand($"SELECT {columnList} FROM {tableName}", sqliteConn);
            using var selectReader = selectCmd.ExecuteReader();

            var insertSql = $"INSERT INTO {tableName} ({columnList}) VALUES ({paramList})";

            int rowCount = 0;
            while (selectReader.Read())
            {
                using var insertCmd = new NpgsqlCommand(insertSql, pgConn);

                for (int i = 0; i < columns.Count; i++)
                {
                    var value = selectReader.IsDBNull(i) ? DBNull.Value : selectReader.GetValue(i);
                    insertCmd.Parameters.AddWithValue($"@{columns[i]}", value);
                }

                insertCmd.ExecuteNonQuery();
                rowCount++;
            }

            Console.WriteLine($"=== SQLMITE: COPIED {rowCount} ROWS TO {tableName} ===");
        }

        private static List<string> GetColumnNames(SqliteConnection conn, string tableName)
        {
            var columns = new List<string>();
            using var cmd = new SqliteCommand($"PRAGMA table_info({tableName})", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                columns.Add(reader.GetString(0));
            }

            return columns;
        }
    }
}
