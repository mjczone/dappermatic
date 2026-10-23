// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections;
using System.Data.Common;

namespace MJCZone.DapperMatic.Dml.Internal;

/// <summary>
/// Rows as a data reader, for the bulk copy APIs that read one: each row's values converted as statements bind
/// them, one row at a time, so that the rows are never all in memory.
/// </summary>
internal sealed class DmlRowReader : DbDataReader
{
    private readonly IEnumerator<object> _rows;
    private readonly string[] _names;
    private readonly Func<object, int, object?> _value;
    private readonly object?[] _values;
    private bool _closed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DmlRowReader"/> class.
    /// </summary>
    /// <param name="rows">The rows.</param>
    /// <param name="names">The column names, as the table names them.</param>
    /// <param name="value">Reads a row's value of a column, converted for the driver.</param>
    public DmlRowReader(IEnumerable<object> rows, string[] names, Func<object, int, object?> value)
    {
        _rows = rows.GetEnumerator();
        _names = names;
        _value = value;
        _values = new object?[names.Length];
    }

    /// <summary>
    /// Gets the number of rows read so far.
    /// </summary>
    public long RowsRead { get; private set; }

    /// <inheritdoc />
    public override int FieldCount => _names.Length;

    /// <inheritdoc />
    public override int Depth => 0;

    /// <inheritdoc />
    public override bool HasRows => true;

    /// <inheritdoc />
    public override bool IsClosed => _closed;

    /// <inheritdoc />
    public override int RecordsAffected => -1;

    /// <inheritdoc />
    public override object this[int ordinal] => GetValue(ordinal);

    /// <inheritdoc />
    public override object this[string name] => GetValue(GetOrdinal(name));

    /// <inheritdoc />
    public override bool Read()
    {
        if (!_rows.MoveNext())
        {
            return false;
        }

        for (var i = 0; i < _values.Length; i++)
        {
            _values[i] = _value(_rows.Current, i);
        }

        RowsRead++;
        return true;
    }

    /// <inheritdoc />
    public override bool NextResult() => false;

    /// <inheritdoc />
    public override object GetValue(int ordinal) => _values[ordinal] ?? DBNull.Value;

    /// <inheritdoc />
    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, _values.Length);
        for (var i = 0; i < count; i++)
        {
            values[i] = GetValue(i);
        }

        return count;
    }

    /// <inheritdoc />
    public override bool IsDBNull(int ordinal) => _values[ordinal] is null;

    /// <inheritdoc />
    public override string GetName(int ordinal) => _names[ordinal];

    /// <inheritdoc />
    public override int GetOrdinal(string name) =>
        Array.FindIndex(_names, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) is var i and >= 0
            ? i
            : throw new IndexOutOfRangeException($"No column is named {name}.");

    /// <inheritdoc />
    public override Type GetFieldType(int ordinal) => _values[ordinal]?.GetType() ?? typeof(object);

    /// <inheritdoc />
    public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;

    /// <inheritdoc />
    public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);

    /// <inheritdoc />
    public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);

    /// <inheritdoc />
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
    {
        var bytes = (byte[])GetValue(ordinal);
        if (buffer is null)
        {
            return bytes.Length;
        }

        var count = (int)Math.Max(0, Math.Min(length, bytes.Length - dataOffset));
        Array.Copy(bytes, dataOffset, buffer, bufferOffset, count);
        return count;
    }

    /// <inheritdoc />
    public override char GetChar(int ordinal) => GetValue(ordinal) is string { Length: > 0 } s ? s[0] : (char)GetValue(ordinal);

    /// <inheritdoc />
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
    {
        var text = GetString(ordinal);
        if (buffer is null)
        {
            return text.Length;
        }

        var count = (int)Math.Max(0, Math.Min(length, text.Length - dataOffset));
        text.CopyTo((int)dataOffset, buffer, bufferOffset, count);
        return count;
    }

    /// <inheritdoc />
    public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);

    /// <inheritdoc />
    public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public override Guid GetGuid(int ordinal) => GetValue(ordinal) is string s ? Guid.Parse(s) : (Guid)GetValue(ordinal);

    /// <inheritdoc />
    public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public override string GetString(int ordinal) => Convert.ToString(GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

    /// <inheritdoc />
    public override IEnumerator GetEnumerator() => new DbEnumerator(this);

    /// <inheritdoc />
    public override void Close() => _closed = true;

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _rows.Dispose();
            _closed = true;
        }

        base.Dispose(disposing);
    }
}
