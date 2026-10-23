# OpenAPI Integration

DapperMatic.AspNetCore describes its endpoints with **standard ASP.NET Core metadata only**.
It references no OpenAPI package of any kind, which means:

- Any OpenAPI generator can document its endpoints — the built-in ASP.NET Core one, Swashbuckle, or NSwag.
- There is no OpenAPI version for it to conflict with, so **any Swashbuckle version works**.
- Adding DapperMatic to your app never drags an OpenAPI dependency into your dependency graph.

## Recommended setup: built-in generator + Scalar

ASP.NET Core 10 generates OpenAPI documents natively — no third-party generator required.

```bash
dotnet add package Microsoft.AspNetCore.OpenApi
dotnet add package Scalar.AspNetCore
```

```csharp
using MJCZone.DapperMatic.AspNetCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDapperMatic(); // Uses in-memory repository by default

builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();

app.UseDapperMatic();

app.Run();
```

You now have:

- **Scalar UI** at `/scalar`
- **OpenAPI document** at `/openapi/v1.json`

`Microsoft.AspNetCore.OpenApi` is maintained alongside ASP.NET Core but ships as its own NuGet
package, so it does need an explicit `PackageReference`. Keep its major version aligned with your
target framework (`10.x` for `net10.0`).

### Customizing the document

Use a document transformer to set the title, description, contact, and license:

```csharp
using Microsoft.OpenApi;

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "My Schema API",
            Version = "v1",
            Description = "Database schema management powered by DapperMatic",
        };
        return Task.CompletedTask;
    });
});
```

## Generating the spec at build time

You can emit the OpenAPI document as a **build artifact**, with no need to start the app or bind a
port. This is how DapperMatic's own documentation is produced.

Add the build-time generator:

```bash
dotnet add package Microsoft.Extensions.ApiDescription.Server
```

Then point it at an output location in your `.csproj`:

```xml
<PropertyGroup>
  <OpenApiDocumentsDirectory>$(MSBuildProjectDirectory)/openapi</OpenApiDocumentsDirectory>
  <OpenApiGenerateDocumentsOptions>--file-name openapi</OpenApiGenerateDocumentsOptions>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.ApiDescription.Server" Version="10.0.11" PrivateAssets="All" />
</ItemGroup>
```

Now `dotnet build` writes `openapi/openapi.json`. This is ideal for CI pipelines that publish an API
reference, diff the schema between commits, or generate clients — none of which should require
running a web server.

## Using Swashbuckle instead

Swashbuckle continues to work, at **any version**:

```csharp
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();
app.UseDapperMatic();
```

| Swashbuckle Version | Microsoft.OpenApi | Status      |
|---------------------|-------------------|-------------|
| 6.5.0 - 6.x         | 1.x               | ✅ Supported |
| 7.x - 9.x           | 1.x               | ✅ Supported |
| 10.0+               | 2.x               | ✅ Supported |

Because DapperMatic contributes no OpenAPI package to your dependency graph, whichever version
Swashbuckle resolves is simply the version you get. There is nothing for NuGet to reconcile.

## OpenAPI 3.1

The built-in generator emits **OpenAPI 3.1**, while Swashbuckle 6.x-9.x emits 3.0. The practical
differences you may notice:

| Concept          | 3.0                  | 3.1                          |
|------------------|----------------------|------------------------------|
| Nullable strings | `"nullable": true`   | `"type": ["null", "string"]` |
| Examples         | `example`            | `examples`                   |

Scalar, Redoc, and current Swagger UI all render 3.1. If a downstream tool requires 3.0, keep using
Swashbuckle 9.x, which still emits 3.0.

## Troubleshooting

### Endpoints missing from the document

Confirm `UseDapperMatic()` runs before `app.Run()`, and that the generator is configured
(`AddOpenApi()` plus `MapOpenApi()`, or `AddSwaggerGen()` plus `UseSwagger()`).

### Version conflict errors with `Microsoft.OpenApi`

These cannot originate from DapperMatic, which references no OpenAPI package. Check for another
dependency pinning a different major version:

```bash
dotnet nuget why <project> Microsoft.OpenApi
```

## Reporting Issues

If you encounter integration issues:

- **Report at**: https://github.com/mjczone/dappermatic/issues
- **Include**: OpenAPI stack and version, .NET version, error messages, steps to reproduce

## Additional Resources

- [DapperMatic GitHub Repository](https://github.com/mjczone/dappermatic)
- [ASP.NET Core OpenAPI documentation](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/overview)
- [Scalar for .NET](https://github.com/scalar/scalar/tree/main/integrations/aspnetcore)
- [Swashbuckle.AspNetCore](https://github.com/domaindrivendev/Swashbuckle.AspNetCore)
