// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Xml.Linq;
using Dapper;

namespace MJCZone.DapperMatic.TypeMapping.Handlers;

/// <summary>
/// Type handler for XDocument to support XML data across all database providers.
/// Serializes XDocument to string for storage and deserializes string back to XDocument.
/// </summary>
public class XDocumentTypeHandler : SqlMapper.TypeHandler<XDocument>
{
    /// <summary>
    /// Sets the parameter value for an XDocument.
    /// Converts XDocument to XML string for database storage.
    /// </summary>
    /// <param name="parameter">The database parameter to set.</param>
    /// <param name="value">The XDocument value to serialize.</param>
    public override void SetValue(IDbDataParameter parameter, XDocument? value)
    {
        if (value == null)
        {
            parameter.Value = DBNull.Value;
        }
        else
        {
            var xmlString = value.ToString();

            // PostgreSQL requires explicit XML type for xml columns
            // Use reflection to set NpgsqlDbType.Xml if available (avoid hard dependency on Npgsql)
            if (parameter.GetType().FullName?.Contains("Npgsql", StringComparison.Ordinal) == true)
            {
                var npgsqlDbTypeProperty = parameter.GetType().GetProperty("NpgsqlDbType");
                if (npgsqlDbTypeProperty != null)
                {
                    // Get the NpgsqlDbType enum and set it to Xml
                    var npgsqlDbTypeEnum = npgsqlDbTypeProperty.PropertyType;
                    var xmlValue = Enum.Parse(npgsqlDbTypeEnum, "Xml");
                    npgsqlDbTypeProperty.SetValue(parameter, xmlValue);
                }
            }

            parameter.Value = xmlString;
        }
    }

    /// <summary>
    /// Parses the database value to an XDocument.
    /// Converts XML string from database back to XDocument.
    /// </summary>
    /// <param name="value">The database value to parse.</param>
    /// <returns>The parsed XDocument, or null if the value is null or DBNull.</returns>
    public override XDocument? Parse(object value)
    {
        if (value == null || value is DBNull)
        {
            return null;
        }

        // If already an XDocument (shouldn't happen, but handle it)
        if (value is XDocument xdoc)
        {
            return xdoc;
        }

        // Parse string to XDocument
        return XDocument.Parse(value.ToString()!);
    }
}
