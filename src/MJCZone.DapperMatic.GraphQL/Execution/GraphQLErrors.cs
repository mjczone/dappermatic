// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using GraphQL;
using MJCZone.DapperMatic.AspNetCore;
using MJCZone.DapperMatic.AspNetCore.Extensions;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Validation;

namespace MJCZone.DapperMatic.GraphQL.Execution;

/// <summary>
/// What goes wrong in a field, as a GraphQL error with a code (<c>extensions.code</c>): <c>FORBIDDEN</c> or
/// <c>UNAUTHENTICATED</c>, <c>BAD_USER_INPUT</c> (Apollo's name), <c>NOT_FOUND</c>, <c>CONFLICT</c>, a database error's
/// class (<c>UNIQUE_VIOLATION</c>, <c>DEADLOCK</c>…), else <c>INTERNAL_SERVER_ERROR</c>. A database's own text, and an
/// unexpected error's, only where error details are included (Development).
/// </summary>
internal static class GraphQLErrors
{
    /// <summary>The code of a field that succeeded in a mutation rolled back because another failed.</summary>
    public const string RolledBack = "ROLLED_BACK";

    /// <summary>
    /// Audits a failed field or request as an endpoint's failure is: as the operation the service received.
    /// </summary>
    /// <param name="services">The request's services.</param>
    /// <param name="operation">The operation the field ran as.</param>
    /// <param name="exception">What went wrong.</param>
    /// <returns>A task.</returns>
    public static Task AuditAsync(IServiceProvider services, DapperMaticOperation operation, Exception exception) =>
        exception is ExecutionError or OperationCanceledException
            ? Task.CompletedTask
            : DapperMaticRequestPipeline.AuditFailureAsync(services, operation, exception);

    /// <summary>
    /// Makes the GraphQL error of an exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="services">The request's services, which say whether details are included.</param>
    /// <returns>The error.</returns>
    public static ExecutionError Of(Exception exception, IServiceProvider services)
    {
        if (exception is ExecutionError error)
        {
            return error;
        }

        var details = ErrorHandler.IncludeDetails(services);
        var signedIn = DapperMaticCallerSession.Current?.User.Identity?.IsAuthenticated == true;
        (string Code, string Message) described = exception switch
        {
            UnauthorizedAccessException when !signedIn => ("UNAUTHENTICATED", exception.Message),
            UnauthorizedAccessException => ("FORBIDDEN", exception.Message),
            ArgumentException or ValidationException or FormatException => ("BAD_USER_INPUT", exception.Message),
            KeyNotFoundException => ("NOT_FOUND", exception.Message),
            DuplicateKeyException or DuplicateNameException => ("CONFLICT", exception.Message),
            _ => Database(exception, details),
        };
        // The exception goes with the error only when details are shown: GraphQL.NET writes a code for each exception
        // it holds, which would name the database's driver.
        return new ExecutionError(described.Message, details ? exception : null) { Code = described.Code };
    }

    private static (string Code, string Message) Database(Exception exception, bool details)
    {
        var kind = DmDatabaseErrors.Classify(exception);
        if (kind != DmDatabaseErrorKind.Unknown || exception is DbException || exception.InnerException is DbException)
        {
            var (_, code, _, message) = ErrorHandler.DatabaseProblem(kind);
            return (code.ToUpperInvariant(), details ? exception.Message : message);
        }

        return exception is InvalidOperationException
            ? ("BAD_REQUEST", exception.Message)
            : ("INTERNAL_SERVER_ERROR", details ? exception.Message : "An unexpected error occurred.");
    }
}
