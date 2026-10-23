// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using Dapper;

namespace MJCZone.DapperMatic.Dml.Internal;

/// <summary>
/// Parameters bound by each value's declared type, the way Dapper binds a model's properties.
/// </summary>
/// <remarks>
/// <see cref="DynamicParameters"/> picks a value's handler from its runtime type, which misses the handler for a
/// property declared as <c>IList&lt;OrderLine&gt;</c> holding a <c>List&lt;OrderLine&gt;</c>. Used where one statement
/// carries several rows, so the model itself cannot be the parameter object.
/// </remarks>
internal sealed class DmlParameters : SqlMapper.IDynamicParameters
{
    private readonly List<(string Name, Type Type, object? Value)> _parameters = [];

    /// <summary>
    /// Gets the number of parameters.
    /// </summary>
    public int Count => _parameters.Count;

    /// <summary>
    /// Gets the parameters' values, in the order they were added.
    /// </summary>
    public IEnumerable<object?> Values => _parameters.Select(p => p.Value);

    /// <summary>
    /// Gets the parameters' names, without a prefix, and values, in the order they were added.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, object?>> Named =>
        [.. _parameters.Select(p => new KeyValuePair<string, object?>(p.Name, p.Value))];

    /// <summary>
    /// Adds a parameter.
    /// </summary>
    /// <param name="name">The name, without a prefix.</param>
    /// <param name="declaredType">The type of the property the value comes from.</param>
    /// <param name="value">The value.</param>
    public void Add(string name, Type declaredType, object? value) => _parameters.Add((name, declaredType, value));

    /// <inheritdoc />
    public void AddParameters(IDbCommand command, SqlMapper.Identity identity)
    {
        foreach (var (name, type, value) in _parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            Bind(parameter, type, value, name);
            command.Parameters.Add(parameter);
        }
    }

    /// <summary>
    /// Sets a parameter's value as Dapper binds a member of a declared type: through the type's handler, an enum as
    /// its underlying integer, a <see cref="char"/> as a string.
    /// </summary>
    /// <param name="parameter">The parameter, of the driver the value is for.</param>
    /// <param name="declaredType">The declared type of the value's property.</param>
    /// <param name="value">The value.</param>
    /// <param name="name">The parameter's name, for messages.</param>
    /// <exception cref="NotSupportedException">The type is a collection Dapper expands rather than stores.</exception>
    public static void Bind(IDbDataParameter parameter, Type declaredType, object? value, string name)
    {
#pragma warning disable CS0618 // "For internal use": it is how Dapper itself binds a member by its declared type.
        var dbType = SqlMapper.LookupDbType(declaredType, name, true, out var handler);
#pragma warning restore CS0618

        if (value is null)
        {
            parameter.Value = DBNull.Value;
        }
        else if (handler is not null)
        {
            handler.SetValue(parameter, value);
        }
        else if (dbType == (DbType)(-1))
        {
            // Dapper's marker for a collection it would expand into a list.
            throw new NotSupportedException(
                $"{name} is a {declaredType.Name}, a collection of simple values, which is not stored as a column unless "
                    + "registered: see DapperMaticTypeMapping.RegisterJsonTypes and StoreSimpleCollectionsAsJson."
            );
        }
        else
        {
            parameter.Value = value switch
            {
                Enum e => Convert.ChangeType(e, Enum.GetUnderlyingType(e.GetType()), System.Globalization.CultureInfo.InvariantCulture),
                char c => c.ToString(),
                _ => value,
            };

            if (dbType is { } known && known != DbType.Object)
            {
                parameter.DbType = known;
            }
        }
    }
}
