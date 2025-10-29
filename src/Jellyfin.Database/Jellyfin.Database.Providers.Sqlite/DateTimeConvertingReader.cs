using System;
using System.Data.Common;

namespace Jellyfin.Database.Providers.Sqlite
{
    internal class DateTimeConvertingReader : DbDataReader
    {
        private readonly DbDataReader _innerReader;
        private readonly string[] _dateTimeColumns = { "DateCreated", "DateLastMediaAdded", "DateLastRefreshed", "DateLastSaved", "DateModified", "EndDate", "PremiereDate", "StartDate" };

        public DateTimeConvertingReader(DbDataReader innerReader)
        {
            _innerReader = innerReader;
        }

        public override int FieldCount => _innerReader.FieldCount;

        public override bool HasRows => _innerReader.HasRows;

        public override bool IsClosed => _innerReader.IsClosed;

        public override int RecordsAffected => _innerReader.RecordsAffected;

        public override int Depth => _innerReader.Depth;

        public override object this[int ordinal] => GetValue(ordinal);

        public override object this[string name] => GetValue(GetOrdinal(name));

        public override object GetValue(int ordinal)
        {
            var columnName = GetName(ordinal);
            var value = _innerReader.GetValue(ordinal);

            // Convert VARCHAR datetime strings to DateTime objects
            if (Array.IndexOf(_dateTimeColumns, columnName) >= 0 && value is string stringValue && !string.IsNullOrEmpty(stringValue))
            {
                if (DateTime.TryParse(stringValue, out var dateTime))
                {
                    return dateTime;
                }
            }

            return value;
        }

        public override bool GetBoolean(int ordinal) => _innerReader.GetBoolean(ordinal);

        public override byte GetByte(int ordinal) => _innerReader.GetByte(ordinal);

        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => _innerReader.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);

        public override char GetChar(int ordinal) => _innerReader.GetChar(ordinal);

        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => _innerReader.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);

        public override string GetDataTypeName(int ordinal) => _innerReader.GetDataTypeName(ordinal);

        public override DateTime GetDateTime(int ordinal) => _innerReader.GetDateTime(ordinal);

        public override decimal GetDecimal(int ordinal) => _innerReader.GetDecimal(ordinal);

        public override double GetDouble(int ordinal) => _innerReader.GetDouble(ordinal);

        public override Type GetFieldType(int ordinal) => _innerReader.GetFieldType(ordinal);

        public override float GetFloat(int ordinal) => _innerReader.GetFloat(ordinal);

        public override Guid GetGuid(int ordinal) => _innerReader.GetGuid(ordinal);

        public override short GetInt16(int ordinal) => _innerReader.GetInt16(ordinal);

        public override int GetInt32(int ordinal) => _innerReader.GetInt32(ordinal);

        public override long GetInt64(int ordinal) => _innerReader.GetInt64(ordinal);

        public override string GetName(int ordinal) => _innerReader.GetName(ordinal);

        public override int GetOrdinal(string name) => _innerReader.GetOrdinal(name);

        public override string GetString(int ordinal) => _innerReader.GetString(ordinal);

        public override int GetValues(object[] values) => _innerReader.GetValues(values);

        public override bool IsDBNull(int ordinal) => _innerReader.IsDBNull(ordinal);

        public override bool NextResult() => _innerReader.NextResult();

        public override bool Read() => _innerReader.Read();

        public override System.Collections.IEnumerator GetEnumerator() => _innerReader.GetEnumerator();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerReader?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
