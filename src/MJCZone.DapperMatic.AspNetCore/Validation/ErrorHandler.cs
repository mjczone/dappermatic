// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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
    /// Handles exceptions and maps them to problem details. Database errors are classified by their codes and get a
    /// message that names no table, column, constraint or value, unless error details are included; any other
    /// unexpected error gets a generic one. Every problem carries a <c>code</c> and the request's <c>traceId</c>.
    /// </summary>
    /// <param name="ex">The exception to handle.</param>
    /// <param name="http">The request, which says whether details are included and who is asking; null includes none.</param>
    /// <returns>An IResult representing the HTTP response.</returns>
    public static IResult HandleError(Exception ex, HttpContext? http = null)
    {
        var includeDetails = http is not null && IncludeDetails(http.RequestServices);
        var traceId = Activity.Current?.Id ?? http?.TraceIdentifier;
        Dictionary<string, object?> Extensions(string code) => new() { ["code"] = code, ["traceId"] = traceId };

        switch (ex)
        {
            // 400
            case ValidationResultException { ValidationResult: not null } dvr:
                return HandleError(dvr.ValidationResult);
            case ValidationResultException dve:
                return Results.ValidationProblem([], detail: dve.Message, statusCode: StatusCodes.Status400BadRequest, extensions: Extensions("invalid_request"));
            case ValidationException ve:
                return Results.ValidationProblem(
                    [],
                    detail: ve.ValidationResult?.ErrorMessage ?? ve.Message,
                    statusCode: StatusCodes.Status400BadRequest,
                    extensions: Extensions("invalid_request")
                );
            case ArgumentException ae:
                return Results.ValidationProblem(
                    !string.IsNullOrWhiteSpace(ae.ParamName) ? new Dictionary<string, string[]> { { ae.ParamName, [ae.Message] } } : [],
                    detail: ae.Message,
                    statusCode: StatusCodes.Status400BadRequest,
                    extensions: Extensions("invalid_request")
                );

            // 401 for a caller who is not signed in, 403 for one who is
            case UnauthorizedAccessException ua when http?.User.Identity?.IsAuthenticated != true:
                return Results.Problem(title: "Unauthorized", detail: ua.Message, statusCode: StatusCodes.Status401Unauthorized, extensions: Extensions("unauthorized"));
            case UnauthorizedAccessException ua:
                return Results.Problem(title: "Forbidden", detail: ua.Message, statusCode: StatusCodes.Status403Forbidden, extensions: Extensions("forbidden"));

            // 404
            case KeyNotFoundException knf:
                return Results.Problem(title: "Not found", detail: knf.Message, statusCode: StatusCodes.Status404NotFound, extensions: Extensions("not_found"));

            // 409
            case DuplicateKeyException dke:
                return Results.Problem(title: "Duplicate key", detail: dke.Message, statusCode: StatusCodes.Status409Conflict, extensions: Extensions("conflict"));
            case DuplicateNameException dne:
                return Results.Problem(title: "Duplicate name", detail: dne.Message, statusCode: StatusCodes.Status409Conflict, extensions: Extensions("conflict"));
        }

        // Database errors, by their codes; their own text only with details included.
        var kind = DmDatabaseErrors.Classify(ex);
        if (kind != DmDatabaseErrorKind.Unknown || ex is DbException || ex.InnerException is DbException)
        {
            var (status, code, title, message) = DatabaseProblem(kind);
            if (status == StatusCodes.Status503ServiceUnavailable && http is not null)
            {
                http.Response.Headers.RetryAfter = "1";
            }

            return Results.Problem(title: title, detail: includeDetails ? ex.Message : message, statusCode: status, extensions: Extensions(code));
        }

        return ex switch
        {
            // 400/409: messages DapperMatic writes itself
            InvalidOperationException ioe => Results.Problem(
                title: "Invalid operation",
                detail: ioe.Message,
                statusCode: (ioe.Message ?? string.Empty).Contains("already exists", StringComparison.OrdinalIgnoreCase)
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status400BadRequest,
                extensions: Extensions("invalid_operation")
            ),

            // 500
            _ => Results.Problem(
                title: "Internal server error",
                detail: includeDetails ? ex.Message : "An unexpected error occurred.",
                statusCode: StatusCodes.Status500InternalServerError,
                extensions: Extensions("internal_error")
            ),
        };
    }

    /// <summary>
    /// A 403 whoever asks: for a refusal that signing in would not change (as sign-up closed to an email).
    /// </summary>
    /// <param name="detail">Why.</param>
    /// <param name="http">The request.</param>
    /// <returns>The problem.</returns>
    internal static IResult Forbidden(string detail, HttpContext? http) =>
        Results.Problem(
            title: "Forbidden",
            detail: detail,
            statusCode: StatusCodes.Status403Forbidden,
            extensions: new Dictionary<string, object?> { ["code"] = "forbidden", ["traceId"] = Activity.Current?.Id ?? http?.TraceIdentifier }
        );

    /// <summary>
    /// Tells whether errors tell their details: <see cref="DapperMaticOptions.IncludeErrorDetails"/>, or in the
    /// Development environment when it is not set.
    /// </summary>
    /// <param name="services">The request's services.</param>
    /// <returns>True if they do.</returns>
    internal static bool IncludeDetails(IServiceProvider? services) =>
        services?.GetService<IOptions<DapperMaticOptions>>()?.Value.IncludeErrorDetails
            ?? services?.GetService<IHostEnvironment>()?.IsDevelopment()
            ?? false;

    /// <summary>
    /// Describes a database error of a kind without naming any table, column, constraint or value.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>Its HTTP status, code, title and message.</returns>
    internal static (int Status, string Code, string Title, string Message) DatabaseProblem(DmDatabaseErrorKind kind) =>
        kind switch
        {
            DmDatabaseErrorKind.UniqueViolation => (409, "unique_violation", "Conflict", "A row with the same key or unique value already exists."),
            DmDatabaseErrorKind.ForeignKeyViolation => (409, "foreign_key_violation", "Conflict", "The row refers to a row that does not exist, or other rows refer to it."),
            DmDatabaseErrorKind.NotNullViolation => (400, "not_null_violation", "Invalid value", "A required value is missing."),
            DmDatabaseErrorKind.CheckViolation => (400, "check_violation", "Invalid value", "A value breaks a rule of the table."),
            DmDatabaseErrorKind.ValueOutOfRange => (400, "value_out_of_range", "Invalid value", "A value is too long or too large for its column."),
            DmDatabaseErrorKind.InvalidValue => (400, "invalid_value", "Invalid value", "A value is not of its column's type."),
            DmDatabaseErrorKind.Deadlock => (503, "deadlock", "Try again", "The database could not complete the request because of other work; try again."),
            DmDatabaseErrorKind.Timeout => (503, "timeout", "Try again", "The database took too long; try again."),
            DmDatabaseErrorKind.PermissionDenied => (403, "database_permission_denied", "Forbidden", "The database refused the operation."),
            DmDatabaseErrorKind.ObjectNotFound => (404, "object_not_found", "Not found", "A table, view or column the request names does not exist."),
            DmDatabaseErrorKind.ConnectionFailed => (503, "database_unavailable", "Unavailable", "The database cannot be reached."),
            _ => (500, "database_error", "Database error", "The database could not complete the request."),
        };
}
