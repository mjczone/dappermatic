// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using System.Text.Json.Nodes;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.Dml.Rest;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Services;

/// <summary>
/// The OpenAPI document of a datasource's rows as one caller may use them.
/// </summary>
public partial class DapperMaticService
{
    private static readonly (string Operation, DmAction Action)[] TableActions =
    [
        ("tables/query", DmAction.Read),
        ("tables/insert-rows", DmAction.Create),
        ("tables/update-rows", DmAction.Update),
        ("tables/delete-rows", DmAction.Delete),
    ];

    /// <inheritdoc />
    public virtual async Task<JsonObject> GetOpenApiDocumentAsync(
        DapperMaticOperation context,
        string datasourceId,
        string? schemaName,
        string serverUrl,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverUrl);
        await AssertPermissionsAsync(context).ConfigureAwait(false);

        // Functions are the datasource's, at /f/{name} beside its tables: the datasource's document has them.
        var withFunctions = schemaName is null;
        schemaName = NormalizeSchemaName(schemaName);
        var paths = new JsonObject();
        var schemas = new JsonObject
        {
            ["Pagination"] = Object(
                new()
                {
                    ["take"] = Type("integer"),
                    ["skip"] = Type("integer"),
                    ["total"] = Type("integer", nullable: true),
                    ["hasMore"] = Type("boolean"),
                    ["nextCursor"] = Type("string", nullable: true),
                }
            ),
            ["Affected"] = Object(new() { ["affected"] = Type("integer") }),
        };

        // Every table and view the caller may do anything to, each with what it may do and the columns it may use.
        var model = await DescribeForCallerAsync(context, datasourceId, schemaName, withFunctions, cancellationToken).ConfigureAwait(false);
        var readable = model.Tables.Where(t => t.May(DmAction.Read)).Select(t => t.Table).ToList();
        foreach (var table in model.Tables)
        {
            AddTable(paths, schemas, datasourceId, table.Table, new Dictionary<DmAction, DmAccessRules>(table.Rules), readable);
        }

        foreach (var function in model.Functions)
        {
            AddFunction(paths, schemas, function);
        }

        return new JsonObject
        {
            ["openapi"] = "3.1.1",
            ["info"] = new JsonObject
            {
                ["title"] = $"{datasourceId}{(schemaName is null ? string.Empty : "." + schemaName)}",
                ["description"] = "The tables and views of the datasource this caller may use, the actions it may take on each, and the columns it may read and write.",
                ["version"] = "1",
            },
            ["servers"] = new JsonArray(new JsonObject { ["url"] = serverUrl }),
            ["paths"] = paths,
            ["components"] = new JsonObject { ["schemas"] = schemas },
        };
    }

    // What the caller may do to a table or view: the permissions the application decides, then the policies.
    private async Task<Dictionary<DmAction, DmAccessRules>> AllowedAsync(
        DapperMaticOperation context,
        IDbConnection connection,
        string datasourceId,
        string? schemaName,
        RestTable table,
        (string Operation, DmAction Action)[] actions
    )
    {
        var allowed = new Dictionary<DmAction, DmAccessRules>();
        var resource = new DmResource(datasourceId, SchemaOf(connection, table.SchemaName ?? schemaName), table.Name, [.. table.Columns.Select(c => c.Name)]);
        foreach (var (operation, action) in actions)
        {
            if (RulesFor(context, resource, action) is not { Allowed: true } rules)
            {
                continue;
            }

            try
            {
                var view = operation.StartsWith("views/", StringComparison.Ordinal);
                await AssertAsAsync(context, operation, schemaName, view ? null : table.Name, view ? table.Name : null, nameof(GetOpenApiDocumentAsync)).ConfigureAwait(false);
                allowed[action] = rules;
            }
            catch (UnauthorizedAccessException)
            {
                // The application's permissions refuse it.
            }
        }

        return allowed;
    }

    // Whether the caller may run a function: as the function endpoint authorizes it, the route naming the function.
    private async Task<bool> MayExecuteAsync(DapperMaticOperation context, string function)
    {
        var asFunction = context with
        {
            RouteValues = new Dictionary<string, string>(context.RouteValues, StringComparer.OrdinalIgnoreCase) { ["functionName"] = function },
        };
        try
        {
            await AssertAsAsync(asFunction, "functions/post", null, null, null, nameof(GetOpenApiDocumentAsync)).ConfigureAwait(false);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void AddFunction(JsonObject paths, JsonObject schemas, Models.DapperMaticFunction function)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        foreach (var parameter in function.Parameters.Where(p => p.Value is null))
        {
            properties[parameter.Name] = ColumnSchema(new RestColumn(parameter.Name, parameter.Type, parameter.Nullable));
            if (!parameter.Nullable && parameter.Default is null)
            {
                required.Add(parameter.Name);
            }
        }

        var arguments = new JsonObject { ["type"] = "object", ["properties"] = properties, ["additionalProperties"] = false };
        if (required.Count > 0)
        {
            arguments["required"] = required;
        }

        schemas[$"{function.Name}Arguments"] = arguments;
        schemas[$"{function.Name}Result"] = Object(
            new() { ["result"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "object" } } }
        );
        var post = Post(
            $"run_{function.Name}",
            "functions",
            function.Description ?? $"Runs {function.Name}",
            new JsonObject { ["$ref"] = $"#/components/schemas/{function.Name}Arguments" },
            $"{function.Name}Result"
        );
        if (required.Count == 0)
        {
            ((JsonObject)post["post"]!["requestBody"]!)["required"] = false;
        }

        paths[$"/f/{function.Name}"] = post;
    }

    private void AddTable(JsonObject paths, JsonObject schemas, string datasourceId, RestTable table, Dictionary<DmAction, DmAccessRules> rules, List<RestTable> readable)
    {
        var name = table.Name;
        var path = $"/{(table.Writable ? "t" : "v")}/{name}";
        IEnumerable<RestColumn> Columns(DmAction action) =>
            table.Columns.Where(c => rules[action].Columns is not { } allowed || allowed.Contains(c.Name));

        if (rules.TryGetValue(DmAction.Read, out _))
        {
            // The row as it is read: its columns, and the relations it may be expanded with, present when expanded.
            var properties = new JsonObject();
            foreach (var column in Columns(DmAction.Read))
            {
                properties[column.Name] = ColumnSchema(column);
            }

            var relations = _options.Relations
                .Where(r =>
                    (r.DatasourceId is null || string.Equals(r.DatasourceId, datasourceId, StringComparison.OrdinalIgnoreCase))
                    && (r.SchemaName is null || string.Equals(r.SchemaName, table.SchemaName, StringComparison.OrdinalIgnoreCase))
                    && string.Equals(r.TableName, name, StringComparison.OrdinalIgnoreCase)
                )
                .Select(r => (Relation: r, Other: readable.FirstOrDefault(t => t.Writable && string.Equals(t.Name, r.OtherTableName, StringComparison.OrdinalIgnoreCase))))
                .Where(r => r.Other is not null)
                .ToList();
            foreach (var (relation, other) in relations)
            {
                var reference = new JsonObject { ["$ref"] = $"#/components/schemas/{other!.Name}" };
                properties[relation.Name] = relation.Many
                    ? new JsonObject { ["type"] = "array", ["items"] = reference, ["description"] = $"The related rows of {other.Name}, when expanded." }
                    : new JsonObject { ["oneOf"] = new JsonArray(reference, Type("null")), ["description"] = $"The related row of {other.Name}, when expanded." };
            }

            schemas[name] = new JsonObject { ["type"] = "object", ["properties"] = properties };
            schemas[$"{name}Page"] = Object(
                new()
                {
                    ["result"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["$ref"] = $"#/components/schemas/{name}" } },
                    ["pagination"] = new JsonObject { ["$ref"] = "#/components/schemas/Pagination" },
                }
            );

            var parameters = new JsonArray(
                Parameter("$filter", "string", "A condition, OData-shaped: `total gt 100 and customer/country eq 'FR'`."),
                Parameter("$orderby", "string", "Columns to order by, each optionally `desc`."),
                Parameter("$select", "string", "The columns to read, comma-separated."),
                Parameter("$top", "integer", "The most rows to read."),
                Parameter("$skip", "integer", "The rows to skip."),
                Parameter("$count", "boolean", "Whether to count every row the condition matches."),
                Parameter("$after", "string", "The cursor of the page before: `pagination.nextCursor`."),
                Parameter("$apply", "string", "Grouping and aggregation: `groupby((status),aggregate(total with sum as sum))`.")
            );
            if (relations.Count > 0)
            {
                parameters.Add(Parameter("$expand", "string", "Relations to read with each row: " + string.Join(", ", relations.Select(r => r.Relation.Name)) + "."));
            }

            if (table.Searchable is not null)
            {
                parameters.Add(Parameter("$search", "string", "Words each row must contain."));
            }

            paths[$"{path}/query"] = new JsonObject
            {
                ["get"] = new JsonObject
                {
                    ["operationId"] = $"query_{name}",
                    ["tags"] = new JsonArray(name),
                    ["summary"] = $"Reads rows of {name}",
                    ["parameters"] = parameters,
                    ["responses"] = Responses($"{name}Page"),
                },
            };
        }

        if (!table.Writable)
        {
            return;
        }

        // The rows written: the columns the caller may write, those the policies set left out.
        JsonObject Written(DmAction action, bool required)
        {
            var properties = new JsonObject();
            var needed = new JsonArray();
            foreach (var column in Columns(action).Where(c => !rules[action].Presets.ContainsKey(c.Name)))
            {
                properties[column.Name] = ColumnSchema(column);
                if (required && !column.Nullable && !column.Filled)
                {
                    needed.Add(column.Name);
                }
            }

            var schema = new JsonObject { ["type"] = "object", ["properties"] = properties };
            if (needed.Count > 0)
            {
                schema["required"] = needed;
            }

            return schema;
        }

        var condition = new JsonObject
        {
            ["filter"] = Type("string", description: "A condition, OData-shaped, of the rows to change."),
            ["where"] = new JsonObject { ["type"] = "object", ["description"] = "The same condition as a JSON tree." },
            ["dryRun"] = Type("boolean", description: "Run, report how many rows would change, and roll back."),
        };

        if (rules.ContainsKey(DmAction.Create))
        {
            schemas[$"{name}New"] = Written(DmAction.Create, required: true);
            schemas[$"{name}Inserted"] = Object(
                new()
                {
                    ["affected"] = Type("integer"),
                    ["rows"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "object" }, ["description"] = "The rows as written: generated keys and defaults filled." },
                }
            );
            var body = Object(
                new()
                {
                    ["rows"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["$ref"] = $"#/components/schemas/{name}New" } },
                    ["dryRun"] = Type("boolean"),
                },
                "rows"
            );
            paths[$"{path}/rows"] = Post($"insert_{name}", name, $"Inserts rows into {name}, all or none", body, $"{name}Inserted");
            if (rules.ContainsKey(DmAction.Update))
            {
                paths[$"{path}/rows/save"] = Post($"save_{name}", name, $"Inserts rows into {name}, or updates those whose key exists", (JsonObject)body.DeepClone(), $"{name}Inserted");
            }
        }

        if (rules.ContainsKey(DmAction.Update))
        {
            schemas[$"{name}Set"] = Written(DmAction.Update, required: false);
            var body = (JsonObject)condition.DeepClone();
            body["set"] = new JsonObject { ["$ref"] = $"#/components/schemas/{name}Set" };
            paths[$"{path}/rows/update"] = Post($"update_{name}", name, $"Updates rows of {name}", new JsonObject { ["type"] = "object", ["properties"] = body, ["required"] = new JsonArray("set") }, "Affected");
        }

        if (rules.ContainsKey(DmAction.Delete))
        {
            paths[$"{path}/rows/delete"] = Post($"delete_{name}", name, $"Deletes rows of {name}", new JsonObject { ["type"] = "object", ["properties"] = condition.DeepClone() }, "Affected");
        }
    }

    // A column's values in JSON Schema: their type, and null when the column allows it.
    private static JsonObject ColumnSchema(RestColumn column)
    {
        var type = Nullable.GetUnderlyingType(column.Type) ?? column.Type;
        (string? Kind, string? Format) info = type switch
        {
            _ when type == typeof(bool) => ("boolean", null),
            _ when type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) || type == typeof(int) => ("integer", "int32"),
            _ when type == typeof(uint) || type == typeof(long) => ("integer", "int64"),
            _ when type == typeof(ulong) => ("integer", null),
            _ when type == typeof(float) => ("number", "float"),
            _ when type == typeof(double) => ("number", "double"),
            _ when type == typeof(decimal) => ("number", null),
            _ when type == typeof(Guid) => ("string", "uuid"),
            _ when type == typeof(DateTime) || type == typeof(DateTimeOffset) => ("string", "date-time"),
            _ when type == typeof(DateOnly) => ("string", "date"),
            _ when type == typeof(TimeOnly) => ("string", "time"),
            _ when type == typeof(byte[]) => ("string", "byte"),
            _ when type == typeof(string) || type == typeof(char) || type == typeof(TimeSpan) => ("string", null),
            _ => (null, null),
        };

        if (info.Kind is null)
        {
            return new JsonObject();
        }

        var schema = Type(info.Kind, column.Nullable);
        if (info.Format is not null)
        {
            schema["format"] = info.Format;
        }

        return schema;
    }

    private static JsonObject Type(string type, bool nullable = false, string? description = null)
    {
        var schema = new JsonObject { ["type"] = nullable ? new JsonArray(type, "null") : type };
        if (description is not null)
        {
            schema["description"] = description;
        }

        return schema;
    }

    private static JsonObject Object(JsonObject properties, params string[] required)
    {
        var schema = new JsonObject { ["type"] = "object", ["properties"] = properties };
        if (required.Length > 0)
        {
            schema["required"] = new JsonArray([.. required.Select(r => (JsonNode)r)]);
        }

        return schema;
    }

    private static JsonObject Parameter(string name, string type, string description) =>
        new()
        {
            ["name"] = name,
            ["in"] = "query",
            ["description"] = description,
            ["schema"] = Type(type),
        };

    private static JsonObject Responses(string schema) =>
        new()
        {
            ["200"] = new JsonObject
            {
                ["description"] = "OK",
                ["content"] = new JsonObject { ["application/json"] = new JsonObject { ["schema"] = new JsonObject { ["$ref"] = $"#/components/schemas/{schema}" } } },
            },
            ["400"] = new JsonObject { ["description"] = "The request names something the table does not have, or asks for too much." },
            ["403"] = new JsonObject { ["description"] = "The policies do not allow it." },
        };

    private static JsonObject Post(string operationId, string tag, string summary, JsonObject body, string response) =>
        new()
        {
            ["post"] = new JsonObject
            {
                ["operationId"] = operationId,
                ["tags"] = new JsonArray(tag),
                ["summary"] = summary,
                ["requestBody"] = new JsonObject
                {
                    ["required"] = true,
                    ["content"] = new JsonObject { ["application/json"] = new JsonObject { ["schema"] = body } },
                },
                ["responses"] = Responses(response),
            },
        };
}
