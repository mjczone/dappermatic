// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.AspNetCore.Validation;

/// <summary>
/// A builder for validating objects of type T.
/// </summary>
/// <typeparam name="T">The type of object to validate.</typeparam>
public class ObjectValidationBuilder<T>
{
    private readonly T _item;
    private readonly Dictionary<string, List<string>> _errors = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectValidationBuilder{T}"/> class.
    /// </summary>
    /// <param name="item">The item to validate.</param>
    public ObjectValidationBuilder(T item) => _item = item;

    /// <summary>
    /// Validates that a property is not null.
    /// </summary>
    /// <typeparam name="TProperty">The type of the property to validate.</typeparam>
    /// <param name="selector">A function to select the property from the item.</param>
    /// <param name="propertyName">The name of the property being validated (for error messages).</param>
    /// <returns>The current <see cref="ObjectValidationBuilder{T}"/> instance for method chaining.</returns>
    public ObjectValidationBuilder<T> NotNull<TProperty>(Func<T, TProperty?> selector, string propertyName)
        where TProperty : class
    {
        var value = selector(_item);

        if (value == null)
        {
            AddOrUpdateError(propertyName, $"{propertyName} is required.");
        }

        return this;
    }

    /// <summary>
    /// Validates that a string property is not null, empty, or whitespace.
    /// </summary>
    /// <param name="selector">A function to select the string property from the item.</param>
    /// <param name="propertyName">The name of the property being validated (for error messages).</param>
    /// <returns>The current <see cref="ObjectValidationBuilder{T}"/> instance for method chaining.</returns>
    public ObjectValidationBuilder<T> NotNullOrWhiteSpace(Func<T, string?> selector, string propertyName)
    {
        var value = selector(_item);

        if (ValidationHelpers.IsNullOrWhiteSpace(value))
        {
            AddOrUpdateError(propertyName, ValidationHelpers.RequiredMessage(propertyName));
        }

        return this;
    }

    /// <summary>
    /// Validates that a string property is not null or empty.
    /// </summary>
    /// <param name="selector">A function to select the string property from the item.</param>
    /// <param name="propertyName">The name of the property being validated (for error messages).</param>
    /// <returns>The current <see cref="ObjectValidationBuilder{T}"/> instance for method chaining.</returns>
    public ObjectValidationBuilder<T> NotNullOrEmpty(Func<T, string?> selector, string propertyName)
    {
        var value = selector(_item);

        if (ValidationHelpers.IsNullOrEmpty(value))
        {
            AddOrUpdateError(propertyName, $"{propertyName} cannot be null or empty.");
        }

        return this;
    }

    /// <summary>
    /// Validates that a string property equals a specified value.
    /// </summary>
    /// <param name="selector">A function to select the string property from the item.</param>
    /// <param name="compareValue">The value to compare against.</param>
    /// <param name="ignoreCase">Whether to ignore case when comparing strings.</param>
    /// <param name="errorMessage">The error message to add if the validation fails.</param>
    /// <returns>The current <see cref="ObjectValidationBuilder{T}"/> instance for method chaining.</returns>
    public ObjectValidationBuilder<T> Equal(
        Func<T, string> selector,
        string compareValue,
        bool ignoreCase,
        string? errorMessage = null
    )
    {
        var value = selector(_item);

        if (
            value?.Equals(
                compareValue,
                ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal
            ) != true
        )
        {
            AddOrUpdateError(
                selector.Method.Name,
                errorMessage ?? $"{selector.Method.Name} must equal {compareValue}."
            );
        }

        return this;
    }

    /// <summary>
    /// Validates that a string property does not equal a specified value.
    /// </summary>
    /// <param name="selector">A function to select the string property from the item.</param>
    /// <param name="compareValue">The value to compare against.</param>
    /// <param name="ignoreCase">Whether to ignore case when comparing strings.</param>
    /// <param name="errorMessage">The error message to add if the validation fails.</param>
    /// <returns>The current <see cref="ObjectValidationBuilder{T}"/> instance for method chaining.</returns>
    public ObjectValidationBuilder<T> NotEqual(
        Func<T, string> selector,
        string compareValue,
        bool ignoreCase,
        string? errorMessage = null
    )
    {
        var value = selector(_item);

        if (
            value?.Equals(
                compareValue,
                ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal
            ) == true
        )
        {
            AddOrUpdateError(
                selector.Method.Name,
                errorMessage ?? $"{selector.Method.Name} must not equal {compareValue}."
            );
        }

        return this;
    }

    /// <summary>
    /// Validates that a string property meets a specified minimum length.
    /// </summary>
    /// <param name="selector">A function to select the string property from the item.</param>
    /// <param name="minLength">The minimum allowed length.</param>
    /// <param name="propertyName">The name of the property being validated (for error messages).</param>
    /// <param name="inclusive">Whether the minimum length is inclusive.</param>
    /// <returns>The current <see cref="ObjectValidationBuilder{T}"/> instance for method chaining.</returns>
    public ObjectValidationBuilder<T> MinLength(
        Func<T, string?> selector,
        int minLength,
        string propertyName,
        bool inclusive = true
    )
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            AddOrUpdateError(
                propertyName,
                $"{propertyName} must be at least {minLength} characters long{(inclusive ? " (inclusive)" : string.Empty)}."
            );
            return this;
        }

        var value = selector(_item);
        if (value?.Length < minLength)
        {
            AddOrUpdateError(
                propertyName,
                $"{propertyName} must be at least {minLength} characters long{(inclusive ? " (inclusive)" : string.Empty)}."
            );
        }

        return this;
    }

    /// <summary>
    /// Validates that a string property does not exceed a specified maximum length.
    /// </summary>
    /// <param name="selector">A function to select the string property from the item.</param>
    /// <param name="maxLength">The maximum allowed length.</param>
    /// <param name="propertyName">The name of the property being validated (for error messages).</param>
    /// <param name="inclusive">Whether the maximum length is inclusive.</param>
    /// <returns>The current <see cref="ObjectValidationBuilder{T}"/> instance for method chaining.</returns>
    public ObjectValidationBuilder<T> MaxLength(
        Func<T, string?> selector,
        int maxLength,
        string propertyName,
        bool inclusive = true
    )
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return this;
        }

        var value = selector(_item);
        if (value?.Length > maxLength)
        {
            AddOrUpdateError(
                propertyName,
                $"{propertyName} must not exceed {maxLength} characters{(inclusive ? " (inclusive)" : string.Empty)}."
            );
        }

        return this;
    }

    /// <summary>
    /// Adds a custom validation rule.
    /// </summary>
    /// <param name="condition">A function that returns true if the item is valid, false otherwise.</param>
    /// <param name="propertyName">The name of the property being validated (for error messages).</param>
    /// <param name="errorMessage">The error message to add if the validation fails.</param>
    /// <returns>The current <see cref="ObjectValidationBuilder{T}"/> instance for method chaining.</returns>
    public ObjectValidationBuilder<T> Custom(
        Func<T, bool> condition,
        string propertyName,
        string errorMessage
    )
    {
        if (!condition(_item))
        {
            AddOrUpdateError(propertyName, errorMessage);
        }

        return this;
    }

    /// <summary>
    /// Builds the validation result.
    /// </summary>
    /// <returns>The validation result.</returns>
    public ValidationResult Build() =>
        _errors.Count != 0 ? ValidationResult.Failure(_errors) : ValidationResult.Success();

    /// <summary>
    /// Asserts that the item is valid, throwing an exception if not.
    /// </summary>
    /// <param name="exceptionType">The type of exception to throw if validation fails.</param>
    public void Assert(ValidationExceptionType exceptionType = ValidationExceptionType.ValidationResult)
    {
        if (_errors.Count == 0)
        {
            return;
        }

        var result = Build();

        switch (exceptionType)
        {
            case ValidationExceptionType.ValidationResult:
                throw new ValidationResultException(result);

            case ValidationExceptionType.Argument:
                var errors = string.Join("; ", result.Errors.SelectMany(kvp => kvp.Value));
                throw new ArgumentException($"Validation failed: {errors}");

            default:
                throw new ArgumentOutOfRangeException(nameof(exceptionType), exceptionType, null);
        }
    }

    private void AddOrUpdateError(string propertyName, string errorMessage)
    {
        if (_errors.TryGetValue(propertyName, out List<string>? value))
        {
            value.Add(errorMessage);
            return;
        }
        _errors.TryAdd(propertyName, new List<string> { errorMessage });
    }
}
