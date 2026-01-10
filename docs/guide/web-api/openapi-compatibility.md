# OpenApi/Swashbuckle Compatibility

DapperMatic.AspNetCore is designed to work with a wide range of OpenApi and Swashbuckle versions, ensuring maximum compatibility with your existing infrastructure.

## Supported Versions

### Swashbuckle.AspNetCore Compatibility

| Swashbuckle Version | Microsoft.OpenApi | Status      |
|---------------------|-------------------|-------------|
| 6.5.0 - 6.x         | 1.x               | ✅ Supported |
| 7.x - 9.x           | 1.x               | ✅ Supported |
| 10.0+               | 2.x               | ✅ Supported |

DapperMatic.AspNetCore uses a flexible version range for `Microsoft.AspNetCore.OpenApi` (`[8.0.20,10.0)`), allowing your application to control which OpenApi version is used through your Swashbuckle dependency.

### How It Works

1. **Your application chooses Swashbuckle version**
   ```xml
   <PackageReference Include="Swashbuckle.AspNetCore" Version="10.1.0" />
   ```

2. **Swashbuckle brings Microsoft.OpenApi** (1.x or 2.x)

3. **DapperMatic.AspNetCore adapts automatically** through its flexible version range

This design prevents dependency conflicts while giving you full control over your Swagger/OpenApi stack.

## Recommended Versions

### For New Projects

We recommend using the latest Swashbuckle with OpenApi 2.x:

```xml
<PackageReference Include="Swashbuckle.AspNetCore" Version="10.1.0" />
```

### For Existing Projects

If you're currently using OpenApi 1.x, you can continue without any changes:

```xml
<PackageReference Include="Swashbuckle.AspNetCore" Version="9.0.6" />
```

DapperMatic.AspNetCore works with both versions seamlessly.

## Installation

Add DapperMatic.AspNetCore to your project:

```bash
dotnet add package MJCZone.DapperMatic.AspNetCore
```

Then configure it in your `Program.cs`:

```csharp
using MJCZone.DapperMatic.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add DapperMatic with your preferred datasource repository
builder.Services.AddDapperMatic()
    .WithInMemoryDatasourceRepository(); // or .WithFileDatasourceRepository() or .WithDatabaseDatasourceRepository()

var app = builder.Build();

// Configure Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Register DapperMatic endpoints
app.UseDapperMatic();

app.Run();
```

## Troubleshooting

### Version Conflict Errors

If you encounter version conflicts with `Microsoft.OpenApi`:

**1. Check your Swashbuckle version:**
```bash
dotnet list package | grep Swashbuckle
```

**2. Ensure consistent OpenApi version:**
- Swashbuckle 6.x-9.x → Uses OpenApi 1.x
- Swashbuckle 10.x+ → Uses OpenApi 2.x

Don't mix Swashbuckle versions that use different OpenApi major versions in the same solution.

**3. Clear NuGet package cache:**
```bash
dotnet nuget locals all --clear
dotnet restore
dotnet build
```

### Migration from OpenApi 1.x to 2.x

If upgrading from Swashbuckle 9.x to 10.x:

**1. Update Swashbuckle:**
```xml
<PackageReference Include="Swashbuckle.AspNetCore" Version="10.1.0" />
```

**2. Rebuild:**
```bash
dotnet clean
dotnet build
```

**3. Test Swagger UI:**
- Navigate to `/swagger` in your application
- Verify all endpoints appear correctly
- Check that DapperMatic endpoints are documented properly

**4. Update custom schema filters (if any):**

If you have custom `ISchemaFilter` implementations, you may need to update them for OpenApi 2.x. The `Apply` method signature changed from:

```csharp
// OpenApi 1.x
public void Apply(OpenApiSchema schema, SchemaFilterContext context)
```

to:

```csharp
// OpenApi 2.x
public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
```

**Note**: DapperMatic.AspNetCore itself requires no changes - it works with both versions automatically.

## Version Policy

DapperMatic.AspNetCore follows these versioning principles:

1. **Flexible Dependencies**: Version ranges allow compatibility with multiple Swashbuckle versions
2. **No Breaking Changes**: Updates maintain backward compatibility within major versions
3. **User Control**: Your application controls the OpenApi version through your Swashbuckle dependency
4. **LTS Support**: Maintains support for .NET 8.0 LTS and newer versions

## Why This Approach?

DapperMatic.AspNetCore uses only ASP.NET Core's OpenApi abstraction layer (`Microsoft.AspNetCore.OpenApi`), not direct Microsoft.OpenApi APIs. This means:

- ✅ Works with both OpenApi 1.x and 2.x
- ✅ No breaking changes when Swashbuckle updates
- ✅ You choose when to upgrade
- ✅ Maximum compatibility

The library uses minimal OpenApi APIs:
- `WithOpenApi()` - ASP.NET Core's abstraction
- `OpenApiString` - Basic type for example values
- Property access on parameters - Stable across versions

This minimal API surface ensures long-term compatibility.

## Reporting Issues

If you encounter compatibility issues with specific versions:

- **Report at**: https://github.com/mjczone/dappermatic/issues
- **Include**:
  - Swashbuckle version
  - .NET version
  - Error messages
  - Steps to reproduce

## Additional Resources

- [DapperMatic GitHub Repository](https://github.com/mjczone/dappermatic)
- [Swashbuckle.AspNetCore Documentation](https://github.com/domaindrivendev/Swashbuckle.AspNetCore)
- [Microsoft.OpenApi Documentation](https://github.com/microsoft/OpenAPI.NET)
- [ASP.NET Core Web API Documentation](https://learn.microsoft.com/en-us/aspnet/core/web-api/)
