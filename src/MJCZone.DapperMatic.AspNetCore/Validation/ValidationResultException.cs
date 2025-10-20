// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Validation;

/// <summary>
/// Exception thrown when validation fails.
/// </summary>
public class ValidationResultException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationResultException"/> class.
    /// </summary>
    public ValidationResultException() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationResultException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public ValidationResultException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationResultException"/> class with a specified
    /// error message and validation result.
    /// </summary>
    /// <param name="validationResult">The validation result associated with the exception.</param>
    public ValidationResultException(ValidationResult validationResult)
        : base("One or more validation errors occurred.")
    {
        ValidationResult = validationResult;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationResultException"/> class with a specified
    /// error message and inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The inner exception that is the cause of the current exception.</param>
    public ValidationResultException(string? message, Exception? innerException)
        : base(message, innerException) { }

    /// <summary>
    /// Gets the validation result associated with the exception.
    /// </summary>
    public ValidationResult? ValidationResult { get; private set; }
}
