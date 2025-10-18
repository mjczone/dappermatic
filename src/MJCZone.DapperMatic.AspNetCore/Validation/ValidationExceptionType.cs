// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Validation;

/// <summary>
/// Specifies the type of exception to throw when validation fails.
/// </summary>
public enum ValidationExceptionType
{
    /// <summary>
    /// Throw a ValidationResultException (default for request validation).
    /// </summary>
    ValidationResult = 0,

    /// <summary>
    /// Throw an ArgumentException (for service method argument validation).
    /// </summary>
    Argument = 1,
}