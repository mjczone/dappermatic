// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Validation;

/// <summary>
/// The result of a validation operation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationResult"/> class.
    /// </summary>
    /// <param name="isValid">A value indicating whether the validation was successful.</param>
    /// <param name="errors">The list of validation error messages, if any.</param>
    /// <returns>A new instance of the <see cref="ValidationResult"/> class.</returns>
    private ValidationResult(bool isValid, Dictionary<string, List<string>>? errors = null)
    {
        IsValid = isValid;
        Errors = errors ?? [];
    }

    /// <summary>
    /// Gets a value indicating whether the validation was successful.
    /// </summary>
    public bool IsValid { get; private set; }

    /// <summary>
    /// Gets the list of validation error messages, if any.
    /// </summary>
    public Dictionary<string, List<string>> Errors { get; private set; } = [];

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <returns>A successful <see cref="ValidationResult"/>.</returns>
    public static ValidationResult Success() => new(true);

    /// <summary>
    /// Creates a failed validation result with the specified errors.
    /// </summary>
    /// <param name="errors">The list of validation error messages.</param>
    /// <returns>A failed <see cref="ValidationResult"/>.</returns>
    public static ValidationResult Failure(Dictionary<string, List<string>> errors) =>
        new(false, errors);
}
