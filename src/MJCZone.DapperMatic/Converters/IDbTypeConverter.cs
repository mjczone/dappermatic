// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Converters;

/// <summary>
/// Interface for a type of database type conversion.
/// </summary>
/// <typeparam name="TSource">Source type.</typeparam>
/// <typeparam name="TTarget">Target type.</typeparam>
// ReSharper disable once TypeParameterCanBeVariant
public interface IDbTypeConverter<TSource, TTarget>
{
    /// <summary>
    /// Tries to convert an object of type <typeparamref name="TSource"/> to an object of type <typeparamref name="TTarget"/>.
    /// </summary>
    /// <param name="source">The object to convert from.</param>
    /// <param name="target">The converted object, if the conversion was successful.</param>
    /// <returns>True if the conversion was successful; otherwise, false.</returns>
    bool TryConvert(TSource source, out TTarget? target);
}
