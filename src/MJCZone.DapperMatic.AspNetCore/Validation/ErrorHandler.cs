// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Http;

namespace MJCZone.DapperMatic.AspNetCore.Validation;

/// <summary>
/// Centralized error handling for mapping exceptions to HTTP results.
/// Conforms to RFC 9457 - Problem Details for HTTP APIs.
/// </summary>
internal static class ErrorHandler
{
    /// <summary>
    /// Attempts to validate the item and returns whether it is valid.
    /// </summary>
    /// <typeparam name="T">The type of the item being validated.</typeparam>
    /// <param name="builder">The validation builder containing the item to validate.</param>
    /// <param name="result">The resulting <see cref="ValidationResult"/>.</param>
    /// <returns>True if the item is valid; otherwise, false.</returns>
    public static bool PassesValidation<T>(this ObjectValidationBuilder<T> builder, out IResult? result)
    {
        result = null;
        var validationResult = builder.Build();
        if (!validationResult.IsValid)
        {
            result = HandleError(validationResult);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Handles validation results and maps them to appropriate HTTP results.
    /// </summary>
    /// <param name="validation">The validation result to handle.</param>
    /// <returns>An IResult representing the HTTP response.</returns>
    public static IResult HandleError(ValidationResult validation)
    {
        return Results.ValidationProblem(
            errors: validation.Errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray()),
            detail: validation.Errors.FirstOrDefault().Value?.FirstOrDefault() ?? null,
            statusCode: StatusCodes.Status400BadRequest
        );
    }

    /// <summary>
    /// Handles exceptions and maps them to appropriate HTTP results.
    /// </summary>
    /// <param name="ex">The exception to handle.</param>
    /// <returns>An IResult representing the HTTP response.</returns>
    public static IResult HandleError(Exception ex)
    {
        return ex switch
        {
            // 400
            ValidationResultException dve => dve.ValidationResult != null
                ? HandleError(dve.ValidationResult)
                : Results.ValidationProblem(
                    errors: new Dictionary<string, string[]> { },
                    detail: dve.Message,
                    statusCode: StatusCodes.Status400BadRequest
                ),
            ValidationException ve => Results.ValidationProblem(
                errors: new Dictionary<string, string[]> { },
                detail: ve.ValidationResult?.ErrorMessage ?? ve.Message,
                statusCode: StatusCodes.Status400BadRequest
            ),
            // 400
            ArgumentException ae => Results.ValidationProblem(
                errors: !string.IsNullOrWhiteSpace(ae.ParamName)
                    ? new Dictionary<string, string[]> { { ae.ParamName, new[] { ae.Message } } }
                    : [],
                detail: ae.Message,
                statusCode: StatusCodes.Status400BadRequest
            ),
            // 400/409
            InvalidOperationException ioe => Results.Problem(
                title: "Invalid operation",
                detail: ioe.Message,
                statusCode: (ioe.Message ?? string.Empty).Contains("already exists", StringComparison.OrdinalIgnoreCase)
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status400BadRequest
            ),
            // 409
            DuplicateKeyException dke => Results.Problem(
                title: "Duplicate key",
                detail: dke.Message,
                statusCode: StatusCodes.Status409Conflict
            ),
            DuplicateNameException dne => Results.Problem(
                title: "Duplicate name",
                detail: dne.Message,
                statusCode: StatusCodes.Status409Conflict
            ),
            // 403
            UnauthorizedAccessException ua => Results.Problem(
                title: "Unauthorized or forbidden",
                detail: ua.Message,
                statusCode: StatusCodes.Status403Forbidden
            ),
            // 404
            KeyNotFoundException knf => Results.Problem(
                title: "Key not found",
                detail: knf.Message,
                statusCode: StatusCodes.Status404NotFound
            ),
            // 500
            _ => Results.Problem(
                title: "Internal server error",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            ),
        };
    }
}
