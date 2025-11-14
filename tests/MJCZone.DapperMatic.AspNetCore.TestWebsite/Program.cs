// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "DapperMatic Test API",
            Version = "v1",
            Description = "Test API for DapperMatic ASP.NET Core Integration",
        }
    );

    // WE DO NOT WANT TO FORCE ANY JSON OPTIONS ON THE HOST APPLICATION
    // Configure Swagger to use string values for enums
    // options.UseInlineDefinitionsForEnums();

    // Add support for string enums in Swagger schema
    options.SchemaFilter<JsonStringEnumSchemaFilter>();
});

// WE DO NOT WANT TO FORCE ANY JSON OPTIONS ON THE HOST APPLICATION
// builder.Services.ConfigureHttpJsonOptions(options =>
// {
//     options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
// });

// Add DapperMatic services with in-memory repository (default)
builder.Services.AddDapperMatic();

// Configure DapperMatic options from the appsettings.json file
builder.Services.Configure<DapperMaticOptions>(builder.Configuration.GetSection("DapperMatic"));

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "DapperMatic Test API v1");
        options.RoutePrefix = string.Empty; // Swagger at root
        // options.SupportedSubmitMethods([]); // Disable "Try it out" button
    });
}

// app.UseHttpsRedirection(); // Removed for local testing

app.UseRouting();

// Map DapperMatic endpoints
app.UseDapperMatic();

app.Run();
