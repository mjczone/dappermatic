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