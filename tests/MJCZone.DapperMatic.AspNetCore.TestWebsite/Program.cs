// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using MJCZone.DapperMatic.AspNetCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Built-in ASP.NET Core OpenAPI document generation (replaces Swashbuckle)
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer(
        (document, context, cancellationToken) =>
        {
            document.Info = new OpenApiInfo
            {
                Title = "DapperMatic Test API",
                Version = "v1",
                Description = "Test API for DapperMatic ASP.NET Core Integration",
            };
            return Task.CompletedTask;
        }
    );

    // WE DO NOT WANT TO FORCE ANY JSON OPTIONS ON THE HOST APPLICATION.
    // Describe enums by their string names in the schema only, rather than registering
    // a JsonStringEnumConverter that would change the host application's serialization.
    options.AddSchemaTransformer(
        (schema, context, cancellationToken) =>
        {
            var type = context.JsonTypeInfo.Type;
            if (type.IsEnum)
            {
                schema.Enum = Enum.GetNames(type).Select(name => (JsonNode)name!).ToList();
                schema.Type = JsonSchemaType.String;
                schema.Format = null;
            }

            return Task.CompletedTask;
        }
    );
});

// Add DapperMatic services with in-memory repository (default), and the drivers this site uses: DapperMatic bundles none.
builder.Services.AddDapperMatic(dm =>
    dm.AddProvider(Npgsql.NpgsqlFactory.Instance)
        .AddProvider(Microsoft.Data.SqlClient.SqlClientFactory.Instance)
        .AddProvider(MySqlConnector.MySqlConnectorFactory.Instance)
        .AddProvider(Microsoft.Data.Sqlite.SqliteFactory.Instance)
);

// Configure DapperMatic options from the appsettings.json file
builder.Services.Configure<DapperMaticOptions>(builder.Configuration.GetSection("DapperMatic"));

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Scalar API reference at the site root
    app.MapScalarApiReference(
        "/",
        options =>
        {
            options.Title = "DapperMatic Test API";
        }
    );
}

// app.UseHttpsRedirection(); // Removed for local testing

app.UseRouting();

// Map DapperMatic endpoints
app.UseDapperMatic();

app.Run();
