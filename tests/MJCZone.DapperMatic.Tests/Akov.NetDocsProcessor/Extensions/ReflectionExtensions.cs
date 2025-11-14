// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Reflection;
using Akov.NetDocsProcessor.Helpers;

namespace Akov.NetDocsProcessor.Extensions;

/// <summary>
/// Extension methods for generating XML documentation comment IDs from reflection members.
/// </summary>
internal static class ReflectionExtensions
{
    /// <summary>
    /// Generates an XML documentation comment ID for a type.
    /// </summary>
    /// <param name="type">The type to generate the comment ID for.</param>
    /// <returns>The XML documentation comment ID string.</returns>
    public static string GenerateXmlCommentId(this Type type) => XmlCommentIdGenerator.GenerateForType(type);

    /// <summary>
    /// Generates an XML documentation comment ID for a method.
    /// </summary>
    /// <param name="method">The method to generate the comment ID for.</param>
    /// <returns>The XML documentation comment ID string.</returns>
    public static string GenerateXmlCommentId(this MethodBase method) =>
        XmlCommentIdGenerator.GenerateForMethod(method);

    /// <summary>
    /// Generates an XML documentation comment ID for a property.
    /// </summary>
    /// <param name="property">The property to generate the comment ID for.</param>
    /// <returns>The XML documentation comment ID string.</returns>
    public static string GenerateXmlCommentId(this PropertyInfo property) =>
        XmlCommentIdGenerator.GenerateForProperty(property);

    /// <summary>
    /// Generates an XML documentation comment ID for a field.
    /// </summary>
    /// <param name="field">The field to generate the comment ID for.</param>
    /// <returns>The XML documentation comment ID string.</returns>
    public static string GenerateXmlCommentId(this FieldInfo field) => XmlCommentIdGenerator.GenerateForField(field);

    /// <summary>
    /// Generates an XML documentation comment ID for an event.
    /// </summary>
    /// <param name="eventInfo">The event to generate the comment ID for.</param>
    /// <returns>The XML documentation comment ID string.</returns>
    public static string GenerateXmlCommentId(this EventInfo eventInfo) =>
        XmlCommentIdGenerator.GenerateForEvent(eventInfo);
}
