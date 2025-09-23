namespace MJCZone.DapperMatic.AspNetCore.Validation;

/// <summary>
/// Static helper class for starting validation.
/// </summary>
public static class Validate
{
    /// <summary>
    /// Starts a new validation for the specified object.
    /// </summary>
    /// <typeparam name="T">The type of the object to validate.</typeparam>
    /// <param name="obj">The object to validate.</param>
    /// <returns>A new <see cref="ObjectValidationBuilder{T}"/> instance.</returns>
    public static ObjectValidationBuilder<T> Object<T>(T obj) => new(obj);

    /// <summary>
    /// Starts a new validation for method arguments.
    /// </summary>
    /// <returns>A new <see cref="ArgumentsValidationBuilder"/> instance.</returns>
    public static ArgumentsValidationBuilder Arguments() => new();
}
