# Validation System

This folder contains the validation infrastructure for DapperMatic AspNetCore. The system provides two distinct validation approaches:

1. **Object Validation** (`ObjectValidationBuilder<T>`) - For validating objects with flexible exception handling
2. **Argument Validation** (`ArgumentsValidationBuilder`) - For validating service method arguments with fail-fast behavior

## Architecture Overview

```
Validate (entry point)
├── Object<T>() → ObjectValidationBuilder<T> (object validation, flexible exceptions)
└── Arguments() → ArgumentsValidationBuilder (argument validation, fail-fast)

ValidationHelpers (shared utilities)
├── Message generation methods
├── String validation helpers
└── Comparison utilities
```

## Adding New Validation Methods

### 1. Adding to ArgumentsValidationBuilder (Service Method Arguments)

For validating service method arguments that should fail immediately on first error:

```csharp
// In ArgumentsValidationBuilder.cs
public ArgumentsValidationBuilder YourValidation<T>(T value, string paramName)
{
    // Perform validation
    if (!isValid)
    {
        // Use appropriate exception type:
        // - ArgumentNullException for null checks
        // - ArgumentOutOfRangeException for range violations
        // - ArgumentException for general validation failures
        throw new ArgumentException(
            ValidationHelpers.YourErrorMessage(paramName, additionalInfo),
            paramName);
    }
    return this;
}
```

**Usage:**
```csharp
Validate.Arguments()
    .YourValidation(value, nameof(value))
    .Assert();
```

### 2. Adding to ValidationBuilder<T> (Request Objects)

For validating request DTOs where you want to collect all errors before reporting:

```csharp
// In ValidationBuilder.cs
public ValidationBuilder<T> YourValidation(
    Func<T, YourType> selector,
    string propertyName,
    YourCriteria criteria)
{
    var value = selector(_item);

    if (!IsValid(value, criteria))
    {
        AddOrUpdateError(propertyName,
            ValidationHelpers.YourErrorMessage(propertyName, criteria));
    }

    return this;
}
```

**Usage:**
```csharp
Validate.Object(request)
    .YourValidation(r => r.Property, nameof(request.Property), criteria)
    .Build(); // Returns ValidationResult with all errors
```

### 3. Adding Shared Validation Logic

If validation logic is used by both builders, add it to `ValidationHelpers.cs`:

```csharp
// In ValidationHelpers.cs
public static bool YourValidationCheck(YourType value, YourCriteria criteria)
{
    // Shared validation logic
    return isValid;
}

public static string YourErrorMessage(string propertyName, YourCriteria criteria)
{
    return $"{propertyName} must satisfy {criteria}.";
}
```

## Exception Mapping

The validation system integrates with the error handling pipeline:

| Exception Type | HTTP Status | Use Case |
|---------------|-------------|----------|
| `ArgumentException` | 400 Bad Request | Invalid argument values |
| `ArgumentNullException` | 400 Bad Request | Null arguments |
| `ArgumentOutOfRangeException` | 400 Bad Request | Out of range values |
| `ValidationResultException` | 400 Bad Request | DTO validation failures |
| `InvalidOperationException` | 400/409 | Business rule violations |
| `DuplicateKeyException` | 409 Conflict | Duplicate resource |
| `UnauthorizedAccessException` | 403 Forbidden | Authorization failures |
| `KeyNotFoundException` | 404 Not Found | Resource not found |

## Best Practices

1. **Use ArgumentsValidationBuilder for service methods**
   - Fail fast on first error
   - Clear parameter names in exceptions
   - Standard .NET exception types

2. **Use ValidationBuilder<T> for request DTOs**
   - Collect all errors before reporting
   - User-friendly error messages
   - Structured error responses

3. **Share common logic via ValidationHelpers**
   - Consistent error messages
   - Reusable validation logic
   - Single source of truth

4. **Follow the authorize-then-validate pattern**
   ```csharp
   // 1. Validate context (required for authorization)
   Validate.Arguments()
       .NotNull(context, nameof(context))
       .Assert();

   // 2. Authorize
   await AssertPermissionsAsync(context);

   // 3. Validate business arguments
   Validate.Arguments()
       .NotNullOrWhiteSpace(name, nameof(name))
       .Assert();
   ```

## API Consistency

Both `ObjectValidationBuilder<T>` and `ArgumentsValidationBuilder` share common validation methods for consistency:

**String Validation:**
- **NotNull** - Validates that a value/property is not null
- **NotNullOrWhiteSpace** - Validates that a string value/property is not null, empty, or whitespace
- **NotNullOrEmpty** - Validates that a string value/property is not null or empty

**Boolean Condition Validation (ArgumentsValidationBuilder only):**
- **IsTrue** - Validates that a condition is true (with both immediate and lazy evaluation)
- **IsFalse** - Validates that a condition is false (with both immediate and lazy evaluation)
- **Custom** - For complex validation scenarios with flexible evaluation

This makes switching between argument validation and object validation seamless.

## Common Validation Patterns

### String Validation
```csharp
// Arguments validation
Validate.Arguments()
    .NotNullOrWhiteSpace(value, nameof(value))
    .MinLength(password, 8, nameof(password))
    .MaxLength(description, 500, nameof(description))
    .Matches(email, @"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$", nameof(email))
    .Assert();

// Request object validation
Validate.Object(request)
    .NotNullOrWhiteSpace(r => r.Name, nameof(request.Name))
    .MinLength(r => r.Description, 10, true, nameof(request.Description))
    .Build();
```

### Range Validation
```csharp
// Arguments validation
Validate.Arguments()
    .InRange(count, 1, 100, nameof(count))
    .GreaterThanOrEqual(price, 0m, nameof(price))
    .Assert();

// Request object validation with custom logic
Validate.Object(request)
    .Custom(r => r.StartDate < r.EndDate,
            "DateRange",
            "Start date must be before end date")
    .Build();
```

### Complex Object Validation
```csharp
// Reuse ValidationBuilder within ArgumentsValidationBuilder
Validate.Arguments()
    .Object(complexObject, nameof(complexObject), builder => builder
        .NotNullOrWhiteSpace(o => o.RequiredField, nameof(complexObject.RequiredField))
        .MinLength(o => o.Name, 3, true, nameof(complexObject.Name)))
    .Assert();
```

### Boolean Condition Validation
```csharp
// Clear semantic intent with IsTrue/IsFalse
Validate.Arguments()
    .IsTrue(startDate < endDate, nameof(startDate), "Start date must be before end date")
    .IsTrue(user.IsActive, nameof(user), "User must be active")
    .IsFalse(user.IsDeleted, nameof(user), "User cannot be deleted")
    .Assert();

// Lazy evaluation versions
Validate.Arguments()
    .IsTrue(() => IsValidBusinessLogic(data), nameof(data), "Business logic validation failed")
    .IsFalse(() => HasConflicts(request), nameof(request), "Request has conflicts")
    .Assert();
```

### Custom Validation with Functions
```csharp
// Custom with lazy evaluation and lazy message
Validate.Arguments()
    .Custom(() => IsValidBusinessLogic(data), nameof(data), () => $"Invalid data: {GetDetailedError(data)}")
    .Assert();

// Pre-evaluated condition (for complex scenarios)
var isValid = complexValidationLogic();
Validate.Arguments()
    .Custom(isValid, nameof(parameter), "Complex validation failed")
    .Assert();
```

## Testing Validation

When adding new validation methods, ensure you:

1. Test the validation logic succeeds for valid input
2. Test the validation logic fails for invalid input
3. Verify the correct exception type is thrown
4. Verify the exception message contains the parameter name
5. Test edge cases (null, empty, boundary values)

Example test:
```csharp
[Test]
public void YourValidation_InvalidValue_ThrowsArgumentException()
{
    // Arrange
    var invalidValue = CreateInvalidValue();

    // Act & Assert
    var ex = Assert.Throws<ArgumentException>(() =>
        Validate.Arguments()
            .YourValidation(invalidValue, "testParam")
            .Assert());

    Assert.That(ex.ParamName, Is.EqualTo("testParam"));
    Assert.That(ex.Message, Does.Contain("expected error text"));
}
```