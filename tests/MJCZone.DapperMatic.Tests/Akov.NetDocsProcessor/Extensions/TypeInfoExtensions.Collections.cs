// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Reflection;

namespace Akov.NetDocsProcessor.Extensions;

internal static partial class TypeInfoExtensions
{
    public static IEnumerable<string> GetAllNamespaces(this List<TypeInfo> types)
        => types
            .Where(t => t.Namespace is not null)
            .GroupBy(t => t.Namespace)
            .Select(g => g.First().Namespace!);
    
    public static IEnumerable<TypeInfo> FilterByNamespace(this List<TypeInfo> types, string @namespace)
        => types.Where(t => t.Namespace == @namespace);
}