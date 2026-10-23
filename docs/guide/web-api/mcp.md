# AI Assistants (MCP)

DapperMatic serves its data as tools over the [Model Context Protocol](https://modelcontextprotocol.io), at `{BasePath}/mcp` (`/api/dm/mcp`). An assistant connected to it (Claude, Copilot, …) can explore and use your data **as the signed-in user**. Policies, tenants and audit apply exactly as they do to the web API.

| Tool | What it does |
|------|--------------|
| `list_datasources` | The datasources the user may see |
| `describe_datasource` | The user's own [OpenAPI document](./endpoints.md#each-caller-s-own-openapi-document) of a datasource: tables, columns, relations, the row actions allowed, functions |
| `query_rows` | Reads rows with `$filter`, `$orderby`, `$select`, `$expand`, `$top`, `$after`, `$search`, `$apply` |
| `insert_rows` | Inserts rows, with related rows nested |
| `update_rows` | Sets columns of the rows a condition matches |
| `delete_rows` | Deletes the rows a condition matches |
| `run_function` | Runs a [declared function](./endpoints.md#functions) |

Write tools are always listed. **The user's policies decide what they may do**: an assistant acting for a user who may only read is refused on every write, and the refusal says so (`Forbidden: …`). Grant AI-driven sessions only what you would grant the user.

## Connecting

The endpoint uses the Streamable HTTP transport, stateless, so it needs no session affinity behind a load balancer. Authenticate it as you do the API, typically with a bearer token. A [personal API key](./authentication.md#personal-keys) suits an assistant well: it acts as its owner, can be limited to reading or to some tables, and can be revoked on its own:

```json
{
  "mcpServers": {
    "dappermatic": {
      "type": "http",
      "url": "https://app.example.com/api/dm/mcp",
      "headers": { "Authorization": "Bearer ${DM_TOKEN}" }
    }
  }
}
```

It is **off unless you turn it on**: `DapperMaticEndpoints.Mcp` is not in the default endpoint sets.

```csharp
builder.Services.Configure<DapperMaticOptions>(options => options.Endpoints |= DapperMaticEndpoints.Mcp);
// or every set: options.Endpoints = DapperMaticEndpoints.All;
```

It is built on Microsoft's `ModelContextProtocol.AspNetCore` package. If your application has an MCP server of its own, registered with `AddMcpServer()`, DapperMatic's tools join it.
