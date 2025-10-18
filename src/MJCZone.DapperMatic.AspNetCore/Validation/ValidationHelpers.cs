// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Validation;

/// <summary>
/// Common validation helper methods to reduce code duplication.
/// </summary>
internal static class ValidationHelpers
{
    /// <summary>
    /// Checks if a string is null, empty, or consists only of white-space characters.
    /// </summary>
    /// <param name="value">The string to check.</param>
    /// <returns>true if the value is null, empty, or consists only of white-space characters; otherwise, false.</returns>
    public static bool IsNullOrWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// Checks if a string is null or empty.
    /// </summary>
    /// <param name="value">The string to check.</param>
    /// <returns>true if the value is null or empty; otherwise, false.</returns>
    public static bool IsNullOrEmpty(string? value) => string.IsNullOrEmpty(value);

    /// <summary>
    /// Generates a standard "required" error message.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns>The error message.</returns>
    public static string RequiredMessage(string propertyName) => $"{propertyName} is required.";

    /// <summary>
    /// Generates a standard "must equal" error message.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="expectedValue">The expected value.</param>
    /// <returns>The error message.</returns>
    public static string MustEqualMessage(string propertyName, object expectedValue) =>
        $"{propertyName} must equal {expectedValue}.";

    /// <summary>
    /// Generates a standard "must not equal" error message.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="unexpectedValue">The unexpected value.</param>
    /// <returns>The error message.</returns>
    public static string MustNotEqualMessage(string propertyName, object unexpectedValue) =>
        $"{propertyName} must not equal {unexpectedValue}.";

    /// <summary>
    /// Generates a standard "minimum length" error message.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="minLength">The minimum length.</param>
    /// <param name="inclusive">Whether the minimum is inclusive.</param>
    /// <returns>The error message.</returns>
    public static string MinLengthMessage(string propertyName, int minLength, bool inclusive) =>
        $"{propertyName} must be at least {minLength} characters long{(inclusive ? " (inclusive)" : string.Empty)}.";

    /// <summary>
    /// Generates a standard "maximum length" error message.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="maxLength">The maximum length.</param>
    /// <param name="inclusive">Whether the maximum is inclusive.</param>
    /// <returns>The error message.</returns>
    public static string MaxLengthMessage(string propertyName, int maxLength, bool inclusive) =>
        $"{propertyName} must not exceed {maxLength} characters{(inclusive ? " (inclusive)" : string.Empty)}.";

    /// <summary>
    /// Generates a standard "greater than" error message.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="min">The minimum value.</param>
    /// <returns>The error message.</returns>
    public static string GreaterThanMessage(string propertyName, object min) =>
        $"{propertyName} must be greater than {min}.";

    /// <summary>
    /// Generates a standard "greater than or equal" error message.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="min">The minimum value.</param>
    /// <returns>The error message.</returns>
    public static string GreaterThanOrEqualMessage(string propertyName, object min) =>
        $"{propertyName} must be greater than or equal to {min}.";

    /// <summary>
    /// Generates a standard "less than" error message.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="max">The maximum value.</param>
    /// <returns>The error message.</returns>
    public static string LessThanMessage(string propertyName, object max) =>
        $"{propertyName} must be less than {max}.";

    /// <summary>
    /// Generates a standard "less than or equal" error message.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="max">The maximum value.</param>
    /// <returns>The error message.</returns>
    public static string LessThanOrEqualMessage(string propertyName, object max) =>
        $"{propertyName} must be less than or equal to {max}.";

    /// <summary>
    /// Generates a standard "in range" error message.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="min">The minimum value.</param>
    /// <param name="max">The maximum value.</param>
    /// <returns>The error message.</returns>
    public static string InRangeMessage(string propertyName, object min, object max) =>
        $"{propertyName} must be between {min} and {max} (inclusive).";

    /// <summary>
    /// Generates a standard "must match pattern" error message.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="pattern">The pattern.</param>
    /// <returns>The error message.</returns>
    public static string MustMatchPatternMessage(string propertyName, string pattern) =>
        $"{propertyName} must match the pattern: {pattern}";

    /// <summary>
    /// Compares two strings with optional case sensitivity.
    /// </summary>
    /// <param name="value1">The first string.</param>
    /// <param name="value2">The second string.</param>
    /// <param name="ignoreCase">Whether to ignore case.</param>
    /// <returns>true if the strings are equal; otherwise, false.</returns>
    public static bool StringEquals(string? value1, string? value2, bool ignoreCase) =>
        string.Equals(
            value1,
            value2,
            ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal
        );
}
