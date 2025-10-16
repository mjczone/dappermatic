// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text.RegularExpressions;

namespace MJCZone.DapperMatic.Security;

/// <summary>
/// Provides SQL expression validation to prevent SQL injection attacks.
/// </summary>
internal static partial class SqlExpressionValidator
{
    /// <summary>
    /// Maximum allowed length for SQL expressions.
    /// </summary>
    private const int MaxExpressionLength = 2000;

    /// <summary>
    /// Dangerous SQL keywords and patterns that are not allowed in expressions.
    /// </summary>
    private static readonly string[] DangerousPatterns =
    [
        "--",
        "/*",
        "*/",
        "xp_",
        "sp_",
        "EXEC",
        "EXECUTE",
        "DECLARE",
        "WHILE",
        "IF",
        "GOTO",
        "WAITFOR",
        "SHUTDOWN",
        "BACKUP",
        "RESTORE",
        "KILL",
        "DBCC",
        "BULK",
        "OPENROWSET",
        "OPENQUERY",
        "OPENDATASOURCE",
        "OPENXML",
        "ALTER",
        "CREATE",
        "DROP",
        "TRUNCATE",
        "MERGE",
    ];

    /// <summary>
    /// Additional dangerous patterns for DDL context (more restrictive).
    /// </summary>
    private static readonly string[] DdlDangerousPatterns =
    [
        ";",
        "INSERT",
        "UPDATE",
        "DELETE",
        "SELECT",
        "UNION",
        "JOIN",
        "FROM",
        "WHERE",
        "INTO",
    ];

    /// <summary>
    /// Validates a view definition SQL expression.
    /// </summary>
    /// <param name="viewDefinition">The view definition SQL.</param>
    /// <param name="parameterName">The parameter name for error reporting.</param>
    /// <exception cref="ArgumentException">Thrown when the expression is invalid or potentially dangerous.</exception>
    public static void ValidateViewDefinition(
        string viewDefinition,
        string parameterName = "viewDefinition"
    )
    {
        ValidateBasicExpression(viewDefinition, parameterName);

        // View definitions must start with SELECT
        var trimmed = viewDefinition.Trim();
        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "View definition must start with SELECT statement",
                parameterName
            );
        }

        // View definitions are less restrictive since they're legitimate SELECT statements
        // Only check for the most dangerous patterns
        var viewDangerousPatterns = new[]
        {
            "--",
            "/*",
            "*/",
            "xp_",
            "sp_",
            "EXEC",
            "EXECUTE",
            "DECLARE",
            "WHILE",
            "IF",
            "GOTO",
            "WAITFOR",
            "SHUTDOWN",
            "BACKUP",
            "RESTORE",
            "KILL",
            "DBCC",
            "BULK",
            "OPENROWSET",
            "OPENQUERY",
            "OPENDATASOURCE",
            "OPENXML",
        };
        ValidateAgainstPatterns(viewDefinition, viewDangerousPatterns, parameterName);

        // Check for multiple statements (semicolon followed by non-whitespace)
        // Allow trailing semicolons as they're valid SQL
        if (viewDefinition.Contains(';', StringComparison.Ordinal))
        {
            var statements = viewDefinition.Split(';', StringSplitOptions.RemoveEmptyEntries);
            if (statements.Length > 1)
            {
                // Check if there are multiple non-empty statements
                var nonEmptyStatements = statements
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Count();
                if (nonEmptyStatements > 1)
                {
                    throw new ArgumentException(
                        "View definition cannot contain multiple SQL statements",
                        parameterName
                    );
                }
            }
        }
    }

    /// <summary>
    /// Validates a check constraint expression.
    /// </summary>
    /// <param name="checkExpression">The check constraint expression.</param>
    /// <param name="parameterName">The parameter name for error reporting.</param>
    /// <exception cref="ArgumentException">Thrown when the expression is invalid or potentially dangerous.</exception>
    public static void ValidateCheckExpression(
        string checkExpression,
        string parameterName = "checkExpression"
    )
    {
        ValidateBasicExpression(checkExpression, parameterName);
        ValidateAgainstPatterns(
            checkExpression,
            DangerousPatterns.Concat(DdlDangerousPatterns).ToArray(),
            parameterName
        );

        // Check constraints should not contain semicolons (multiple statements)
        if (checkExpression.Contains(';', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Check constraint expression cannot contain multiple statements",
                parameterName
            );
        }
    }

    /// <summary>
    /// Validates a default constraint expression.
    /// </summary>
    /// <param name="defaultExpression">The default constraint expression.</param>
    /// <param name="parameterName">The parameter name for error reporting.</param>
    /// <exception cref="ArgumentException">Thrown when the expression is invalid or potentially dangerous.</exception>
    public static void ValidateDefaultExpression(
        string defaultExpression,
        string parameterName = "defaultExpression"
    )
    {
        ValidateBasicExpression(defaultExpression, parameterName);
        ValidateAgainstPatterns(
            defaultExpression,
            DangerousPatterns.Concat(DdlDangerousPatterns).ToArray(),
            parameterName
        );

        // Default expressions should not contain semicolons (multiple statements)
        if (defaultExpression.Contains(';', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Default constraint expression cannot contain multiple statements",
                parameterName
            );
        }
    }

    /// <summary>
    /// Performs basic validation on any SQL expression.
    /// </summary>
    /// <param name="expression">The expression to validate.</param>
    /// <param name="parameterName">The parameter name for error reporting.</param>
    /// <exception cref="ArgumentException">Thrown when the expression is invalid.</exception>
    private static void ValidateBasicExpression(string expression, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new ArgumentException("Expression cannot be null or empty", parameterName);
        }

        if (expression.Length > MaxExpressionLength)
        {
            throw new ArgumentException(
                $"Expression too long (max {MaxExpressionLength} characters)",
                parameterName
            );
        }

        // Check for SQL comments before removing them
        if (
            expression.Contains("--", StringComparison.Ordinal)
            || expression.Contains("/*", StringComparison.Ordinal)
        )
        {
            throw new ArgumentException(
                "Expression contains potentially dangerous SQL pattern: comment",
                parameterName
            );
        }

        // Remove SQL comments to prevent comment-based injection
        var sanitized = RemoveSqlComments(expression);

        // Check for null bytes and other control characters
        if (sanitized.Any(c => c < 32 && c != '\t' && c != '\n' && c != '\r'))
        {
            throw new ArgumentException(
                "Expression contains invalid control characters",
                parameterName
            );
        }
    }

    /// <summary>
    /// Validates an expression against dangerous patterns.
    /// </summary>
    /// <param name="expression">The expression to validate.</param>
    /// <param name="patterns">The dangerous patterns to check for.</param>
    /// <param name="parameterName">The parameter name for error reporting.</param>
    /// <exception cref="ArgumentException">Thrown when dangerous patterns are found.</exception>
    private static void ValidateAgainstPatterns(
        string expression,
        string[] patterns,
        string parameterName
    )
    {
        var sanitized = RemoveSqlComments(expression);

        foreach (var pattern in patterns)
        {
            if (ContainsDangerousPattern(sanitized, pattern))
            {
                throw new ArgumentException(
                    $"Expression contains potentially dangerous SQL pattern: {pattern}",
                    parameterName
                );
            }
        }
    }

    /// <summary>
    /// Checks if the expression contains a dangerous pattern, accounting for word boundaries.
    /// </summary>
    /// <param name="expression">The expression to check.</param>
    /// <param name="pattern">The pattern to look for.</param>
    /// <returns>True if the dangerous pattern is found.</returns>
    private static bool ContainsDangerousPattern(string expression, string pattern)
    {
        // Check if pattern is purely alphabetic (only contains letters)
        var isAlphabeticOnly = pattern.All(char.IsLetter);

        // For purely alphabetic patterns, use word boundary matching to avoid false positives
        // (e.g., "IF" should not match "Modified")
        if (isAlphabeticOnly)
        {
            var regex = new Regex($@"\b{Regex.Escape(pattern)}\b", RegexOptions.IgnoreCase);
            return regex.IsMatch(expression);
        }

        // For patterns with special characters (e.g., "xp_", "sp_", "--", "/*"),
        // use simple contains check
        return expression.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Removes SQL comments from an expression to prevent comment-based injection.
    /// </summary>
    /// <param name="expression">The expression to clean.</param>
    /// <returns>The expression with comments removed.</returns>
    private static string RemoveSqlComments(string expression)
    {
        // Remove single-line comments
        expression = SingleLineCommentsRegex().Replace(expression, string.Empty);

        // Remove multi-line comments
        expression = MultiLineCommentsRegex().Replace(expression, string.Empty);

        return expression.Trim();
    }

    [GeneratedRegex(@"--.*$", RegexOptions.Multiline)]
    private static partial Regex SingleLineCommentsRegex();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex MultiLineCommentsRegex();
}
