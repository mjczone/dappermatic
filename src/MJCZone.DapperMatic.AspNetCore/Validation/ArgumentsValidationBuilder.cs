// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace MJCZone.DapperMatic.AspNetCore.Validation;

/// <summary>
/// A builder for validating method arguments with immediate fail-fast behavior.
/// </summary>
public class ArgumentsValidationBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArgumentsValidationBuilder"/> class.
    /// </summary>
    public ArgumentsValidationBuilder() { }

    /// <summary>
    /// Validates that an argument is not null.
    /// </summary>
    /// <typeparam name="T">The type of the argument.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <returns>The current <see cref="ArgumentsValidationBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
    public ArgumentsValidationBuilder NotNull<T>([NotNull] T? value, string paramName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value, paramName);
        return this;
    }

    /// <summary>
    /// Validates that a string argument is not null, empty, or whitespace.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <returns>The current <see cref="ArgumentsValidationBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the value is null, empty, or whitespace.</exception>
    public ArgumentsValidationBuilder NotNullOrWhiteSpace([NotNull] string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return this;
    }

    /// <summary>
    /// Validates that a string argument is not null or empty.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <returns>The current <see cref="ArgumentsValidationBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the value is null or empty.</exception>
    public ArgumentsValidationBuilder NotNullOrEmpty([NotNull] string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrEmpty(value, paramName);
        return this;
    }

    /// <summary>
    /// Validates that a value is greater than a specified minimum.
    /// </summary>
    /// <typeparam name="T">The type of the value (must be comparable).</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="min">The minimum value (exclusive).</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <returns>The current <see cref="ArgumentsValidationBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not greater than the minimum.</exception>
    public ArgumentsValidationBuilder GreaterThan<T>(T value, T min, string paramName)
        where T : IComparable<T>
    {
        if (value.CompareTo(min) <= 0)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                ValidationHelpers.GreaterThanMessage("Value", min)
            );
        }
        return this;
    }

    /// <summary>
    /// Validates that a value is greater than or equal to a specified minimum.
    /// </summary>
    /// <typeparam name="T">The type of the value (must be comparable).</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="min">The minimum value (inclusive).</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <returns>The current <see cref="ArgumentsValidationBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not greater than or equal to the minimum.</exception>
    public ArgumentsValidationBuilder GreaterThanOrEqual<T>(T value, T min, string paramName)
        where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                ValidationHelpers.GreaterThanOrEqualMessage("Value", min)
            );
        }
        return this;
    }

    /// <summary>
    /// Validates that a value is less than a specified maximum.
    /// </summary>
    /// <typeparam name="T">The type of the value (must be comparable).</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="max">The maximum value (exclusive).</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <returns>The current <see cref="ArgumentsValidationBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not less than the maximum.</exception>
    public ArgumentsValidationBuilder LessThan<T>(T value, T max, string paramName)
        where T : IComparable<T>
    {
        if (value.CompareTo(max) >= 0)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                ValidationHelpers.LessThanMessage("Value", max)
            );
        }
        return this;
    }

    /// <summary>
    /// Validates that a value is less than or equal to a specified maximum.
    /// </summary>
    /// <typeparam name="T">The type of the value (must be comparable).</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="max">The maximum value (inclusive).</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <returns>The current <see cref="ArgumentsValidationBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not less than or equal to the maximum.</exception>
    public ArgumentsValidationBuilder LessThanOrEqual<T>(T value, T max, string paramName)
        where T : IComparable<T>
    {
        if (value.CompareTo(max) > 0)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                ValidationHelpers.LessThanOrEqualMessage("Value", max)
            );
        }
        return this;
    }

    /// <summary>
    /// Validates that a value is within a specified range.
    /// </summary>
    /// <typeparam name="T">The type of the value (must be comparable).</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="min">The minimum value (inclusive).</param>
    /// <param name="max">The maximum value (inclusive).</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <returns>The current <see cref="ArgumentsValidationBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside the specified range.</exception>
    public ArgumentsValidationBuilder InRange<T>(T value, T min, T max, string paramName)
        where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                ValidationHelpers.InRangeMessage("Value", min, max)
            );
        }
        return this;
    }

    /// <summary>
    /// Validates that a string has a minimum length.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="minLength">The minimum length.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <param name="inclusive">Whether the minimum length is inclusive (default: true).</param>
    /// <returns>The current <see cref="ArgumentsValidationBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the string is too short.</exception>
    public ArgumentsValidationBuilder MinLength(
        string? value,
        int minLength,
        string paramName,
        bool inclusive = true
    )
    {
        var isValid = inclusive
            ? value != null && value.Length >= minLength
            : value != null && value.Length > minLength;

        if (!isValid)
        {
            throw new ArgumentException(
                ValidationHelpers.MinLengthMessage(paramName, minLength, inclusive),
                paramName
            );
        }
        return this;
    }

    /// <summary>
    /// Validates that a string has a maximum length.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="maxLength">The maximum length.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <param name="inclusive">Whether the maximum length is inclusive (default: true).</param>
    /// <returns>The current <see cref="ArgumentsValidationBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the string is too long.</exception>
    public ArgumentsValidationBuilder MaxLength(
        string? value,
        int maxLength,
        string paramName,
        bool inclusive = true
    )
    {
        var isValid = inclusive
            ? value == null || value.Length <= maxLength
            : value == null || value.Length < maxLength;

        if (!isValid)
        {
            throw new ArgumentException(
                ValidationHelpers.MaxLengthMessage(paramName, maxLength, inclusive),
                paramName
            );
        }
        return this;
    }

    /// <summary>
    /// Validates that a string value matches a specified regular expression pattern.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="pattern">The regular expression pattern.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <returns>The current <see cref="ArgumentsValidationBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the value doesn't match the pattern.</exception>
    public ArgumentsValidationBuilder Matches(
        [NotNull] string? value,
        string pattern,
        string paramName
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(value, paramName);

        if (!Regex.IsMatch(value, pattern))
        {
            throw new ArgumentException(
                ValidationHelpers.MustMatchPatternMessage("Value", pattern),
                paramName
            );
        }
        return this;
    }

    /// <summary>
    /// Validates a complex object using the ObjectValidationBuilder.
    /// </summary>
    /// <typeparam name="T">The type of the object to validate.</typeparam>
    /// <param name="item">The item to validate.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <param name="configure">An action to configure the ObjectValidationBuilder.</param>
    /// <returns>The current <see cref="ArgumentsValidationBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public ArgumentsValidationBuilder Object<T>(
        T item,
        string paramName,
        Action<ObjectValidationBuilder<T>> configure
    )
    {
        ArgumentNullException.ThrowIfNull(item, paramName);

        try
        {
            var builder = new ObjectValidationBuilder<T>(item);
            configure(builder);
            builder.Assert(ValidationExceptionType.Argument);
        }
        catch (ArgumentException ex)
        {
            // Re-throw with correct parameter name context
            throw new ArgumentException(ex.Message, paramName, ex);
        }

        return this;
    }

    /// <summary>
    /// Validates that a condition is true.
    /// </summary>
    /// <param name="condition">The condition that must be true for validation to pass.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <param name="message">The error message if validation fails.</param>
    /// <returns>The current <see cref="ArgumentsValidationBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the condition is false.</exception>
    public ArgumentsValidationBuilder IsTrue(bool condition, string paramName, string message)
    {
        if (!condition)
        {
            throw new ArgumentException(message, paramName);
        }
        return this;
    }

    /// <summary>
    /// Validates that a condition is false.
    /// </summary>
    /// <param name="condition">The condition that must be false for validation to pass.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <param name="message">The error message if validation fails.</param>
    /// <returns>The current <see cref="ArgumentsValidationBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the condition is true.</exception>
    public ArgumentsValidationBuilder IsFalse(bool condition, string paramName, string message)
    {
        if (condition)
        {
            throw new ArgumentException(message, paramName);
        }
        return this;
    }

    /// <summary>
    /// Performs a custom validation with a condition.
    /// </summary>
    /// <param name="condition">The condition that must be true for validation to pass.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <param name="message">The error message if validation fails.</param>
    /// <returns>The current <see cref="ArgumentsValidationBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the condition is false.</exception>
    public ArgumentsValidationBuilder Custom(bool condition, string paramName, string message)
    {
        if (!condition)
        {
            throw new ArgumentException(message, paramName);
        }
        return this;
    }

    /// <summary>
    /// Validates that a lazily-evaluated condition is true.
    /// </summary>
    /// <param name="condition">A function that returns true if validation passes.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <param name="message">The error message if validation fails.</param>
    /// <returns>The current <see cref="ArgumentsValidationBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the condition evaluates to false.</exception>
    public ArgumentsValidationBuilder IsTrue(Func<bool> condition, string paramName, string message)
    {
        ArgumentNullException.ThrowIfNull(condition, nameof(condition));

        if (!condition())
        {
            throw new ArgumentException(message, paramName);
        }
        return this;
    }

    /// <summary>
    /// Validates that a lazily-evaluated condition is false.
    /// </summary>
    /// <param name="condition">A function that returns true if the condition being tested is true.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <param name="message">The error message if validation fails.</param>
    /// <returns>The current <see cref="ArgumentsValidationBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the condition evaluates to true.</exception>
    public ArgumentsValidationBuilder IsFalse(
        Func<bool> condition,
        string paramName,
        string message
    )
    {
        ArgumentNullException.ThrowIfNull(condition, nameof(condition));

        if (condition())
        {
            throw new ArgumentException(message, paramName);
        }
        return this;
    }

    /// <summary>
    /// Performs a custom validation with a lazy condition evaluation.
    /// </summary>
    /// <param name="condition">A function that returns true if validation passes.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <param name="message">The error message if validation fails.</param>
    /// <returns>The current <see cref="ArgumentsValidationBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the condition evaluates to false.</exception>
    public ArgumentsValidationBuilder Custom(Func<bool> condition, string paramName, string message)
    {
        ArgumentNullException.ThrowIfNull(condition, nameof(condition));

        if (!condition())
        {
            throw new ArgumentException(message, paramName);
        }
        return this;
    }

    /// <summary>
    /// Performs a custom validation with a lazy condition and message evaluation.
    /// </summary>
    /// <param name="condition">A function that returns true if validation passes.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <param name="getMessage">A function that returns the error message if validation fails.</param>
    /// <returns>The current <see cref="ArgumentsValidationBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the condition evaluates to false.</exception>
    public ArgumentsValidationBuilder Custom(
        Func<bool> condition,
        string paramName,
        Func<string> getMessage
    )
    {
        ArgumentNullException.ThrowIfNull(condition, nameof(condition));
        ArgumentNullException.ThrowIfNull(getMessage, nameof(getMessage));

        if (!condition())
        {
            throw new ArgumentException(getMessage(), paramName);
        }
        return this;
    }

    /// <summary>
    /// Completes the validation chain. This method does nothing as all validations
    /// are performed immediately (fail-fast), but is provided for API consistency.
    /// </summary>
    public void Assert()
    {
        // All validations are performed immediately in each method (fail-fast behavior)
        // This method is provided for API consistency and to mark the end of the validation chain
    }
}
