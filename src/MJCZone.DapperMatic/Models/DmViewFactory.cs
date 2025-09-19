// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Reflection;
using MJCZone.DapperMatic.DataAnnotations;

namespace MJCZone.DapperMatic.Models;

/// <summary>
/// Factory class for creating and caching instances of <see cref="DmView"/>.
/// </summary>
public static class DmViewFactory
{
    /// <summary>
    /// Cache for storing created <see cref="DmView"/> instances.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, DmView> Cache = new();

    /// <summary>
    /// Returns an instance of a <see cref="DmView"/> for the given type. If the type is not a valid <see cref="DmView"/>,
    /// denoted by the use of a <see cref="DmViewAttribute"/> on the class, this method returns null.
    /// </summary>
    /// <param name="type">The type for which to get the <see cref="DmView"/>.</param>
    /// <returns>An instance of <see cref="DmView"/> if the type is valid; otherwise, null.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the type is missing a view definition.</exception>
    public static DmView? GetView(Type type)
    {
        if (Cache.TryGetValue(type, out var view))
        {
            return view;
        }

        var viewAttribute = type.GetCustomAttribute<DmViewAttribute>();
        if (viewAttribute == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(viewAttribute.Definition))
        {
            throw new InvalidOperationException("Type is missing a view definition.");
        }

        view = new DmView(
            string.IsNullOrWhiteSpace(viewAttribute.SchemaName) ? null : viewAttribute.SchemaName,
            string.IsNullOrWhiteSpace(viewAttribute.ViewName) ? type.Name : viewAttribute.ViewName,
            viewAttribute.Definition.Trim()
        );

        Cache.TryAdd(type, view);
        return view;
    }
}
