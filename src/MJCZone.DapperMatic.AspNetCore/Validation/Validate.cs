// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Validation;

/// <summary>
/// Static helper class for starting validation.
/// </summary>
public static class Validate
{
    /// <summary>
    /// Starts a new validation for the specified object.
    /// </summary>
    /// <typeparam name="T">The type of the object to validate.</typeparam>
    /// <param name="obj">The object to validate.</param>
    /// <returns>A new <see cref="ObjectValidationBuilder{T}"/> instance.</returns>
    public static ObjectValidationBuilder<T> Object<T>(T obj) => new(obj);

    /// <summary>
    /// Starts a new validation for method arguments.
    /// </summary>
    /// <returns>A new <see cref="ArgumentsValidationBuilder"/> instance.</returns>
    public static ArgumentsValidationBuilder Arguments() => new();
}
