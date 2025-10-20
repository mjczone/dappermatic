// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Reflection;
using Akov.NetDocsProcessor.Input;

namespace Akov.NetDocsProcessor.Extensions;

internal static partial class AccessLevelExtensions
{
    public static IEnumerable<TypeInfo> OnlyVisible(this Assembly assembly, AccessLevel accessLevel) =>
        assembly.DefinedTypes.Where(type => type.IsVisibleFor(accessLevel));

    public static IEnumerable<ConstructorInfo> OnlyVisible(
        this ConstructorInfo[] constructors,
        AccessLevel accessLevel
    ) => constructors.Where(ctor => ctor.IsVisibleFor(accessLevel));

    public static IEnumerable<FieldInfo> OnlyVisible(this FieldInfo[] fields, AccessLevel accessLevel) =>
        fields.Where(field => field.IsVisibleFor(accessLevel));

    public static IEnumerable<MethodInfo> OnlyVisible(this MethodInfo[] methods, AccessLevel accessLevel) =>
        methods.Where(method => method.IsVisibleFor(accessLevel));

    public static IEnumerable<PropertyInfo> OnlyVisible(this PropertyInfo[] properties, AccessLevel accessLevel) =>
        properties.Where(prop => prop.IsVisibleFor(accessLevel));

    public static IEnumerable<EventInfo> OnlyVisible(this EventInfo[] events, AccessLevel accessLevel) =>
        events.Where(@event => @event.IsVisibleFor(accessLevel));
}
