// Sample ASP.NET Core app used to generate the OpenAPI specification for documentation.
// The document is written at BUILD time by Microsoft.Extensions.ApiDescription.Server --
// see OpenApiDocumentsDirectory in sample-app.csproj. The app never needs to be run.
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi;
using MJCZone.DapperMatic.AspNetCore;
using MJCZone.DapperMatic.Security;

var builder = WebApplication.CreateBuilder(args);

// Configure to listen on port 5000
builder.WebHost.UseUrls("http://localhost:5000");

// Add services to the container
// Nothing is allowed until a policy grants it. API keys and built-in accounts (with JWT access tokens) are on, so
// that their endpoints are in the document.
builder.Services.AddDapperMatic(dm => dm
    .Policy(DmPolicy.Allow(DmAction.All, "admin"))
    .AddApiKeys()
    .AddUserAccounts(a =>
    {
        a.UseDatabase("Sqlite", "Data Source=accounts.db");
        a.KeepDataProtectionKeys = false;
        a.UseJwt(j =>
        {
            j.Issuer = "http://localhost:5000/api/dm/auth";
            j.Audience = "dappermatic-sample";
            j.AddKey("sample", RSA.Create(2048).ExportPkcs8PrivateKeyPem()); // a real application keeps its key
        });
    }));

// The build starts the app to write the document (as GetDocument.Insider): its hosted services, which would make the
// accounts' tables, are left out then.
if (Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider")
{
    builder.Services.RemoveAll<IHostedService>();
}

// Built-in ASP.NET Core OpenAPI document generation (replaces Swashbuckle)
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer(
        (document, context, cancellationToken) =>
        {
            document.Info = new OpenApiInfo
            {
                Title = "DapperMatic REST API",
                Version = "v1",
                Description = "Database schema management REST API using DapperMatic",
                Contact = new OpenApiContact
                {
                    Name = "MJCZone Inc.",
                    Url = new Uri("https://github.com/mjczone/dappermatic"),
                },
                License = new OpenApiLicense
                {
                    Name = "LGPL v3",
                    Url = new Uri("https://www.gnu.org/licenses/lgpl-3.0.html"),
                },
            };
            return Task.CompletedTask;
        }
    );

    // An enum written by name is a string: say so, as some OpenAPI readers require a type beside the names.
    options.AddSchemaTransformer(
        (schema, context, cancellationToken) =>
        {
            if (context.JsonTypeInfo.Type.IsEnum && schema.Enum is { Count: > 0 } && schema.Type is null)
            {
                schema.Type = JsonSchemaType.String;
            }

            return Task.CompletedTask;
        }
    );
});

var app = builder.Build();

// Serve the document at runtime too, for anyone who wants to browse it locally
app.MapOpenApi();

app.UseRouting();

// Map DapperMatic endpoints
app.UseDapperMatic();

app.Run();
