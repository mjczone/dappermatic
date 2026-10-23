# GraphQL

DapperMatic can serve your data over GraphQL as well as REST, from the package `MJCZone.DapperMatic.GraphQL` (built on [GraphQL.NET](https://graphql-dotnet.github.io), MIT). It is **off unless you add it**.

Every caller gets **their own schema**: only the tables, views, columns, relations, writes and functions the policies let them use. Queries, writes and subscriptions run through the same engine as the REST endpoints, so policies (filters, checks, hidden columns, presets), tenants, the database's own row-level security, the result cache, webhooks, realtime and audit all apply unchanged.

It works with **Apollo Client**, **Relay** and **Strawberry Shake** with one line of client configuration each (see [Clients](#clients)).

## Setup

```bash
dotnet add package MJCZone.DapperMatic.GraphQL
```

```csharp
builder.Services.AddDapperMatic(dm => dm.Policy(DmPolicy.Allow(DmAction.Read, "authenticated")));
builder.Services.AddDapperMaticGraphQL();

app.UseAuthentication();
app.UseDapperMatic();   // also serves GraphQL, and adds WebSockets for subscriptions
```

The endpoint is `{BasePath}/d/{datasourceId}/graphql` (`/api/dm/d/main/graphql`), and `{BasePath}/d/{datasourceId}/s/{schemaName}/graphql` for another schema (the caller's tenant's when none is named, as for every endpoint). It takes:

- **POST** with `{ "query", "operationName", "variables" }` as JSON.
- **GET** with `query`, `operationName` and `variables` in the query string, for queries only. A mutation over GET is refused, 405 with `Allow: POST`.
- **WebSockets**, subprotocol `graphql-transport-ws` (the `graphql-ws` library's protocol), for subscriptions, and for queries and mutations too.

Responses follow [GraphQL over HTTP](https://graphql.github.io/graphql-over-http/draft/): a client accepting `application/graphql-response+json` gets 400 for a document that does not parse and 422 for one that does not validate; with `application/json`, errors come with 200.

## The schema

Names are the database's. A character GraphQL cannot take becomes `_` (`order lines` is `order_lines`), and a name that would clash with another (a table named `orders_connection`, say) gets `_2` after it; the server logs it.

For a table `orders` the caller may read:

```graphql
type orders implements Node {
  nodeId: ID!          # the row's global id (tables with a key the caller may read)
  id: Int!
  total: Decimal!
  status: String!
  customer: customers                  # a declared relation to one row
  lines(where: lines_where, filter: String, orderBy: [lines_order_by!], first: Int, skip: Int): [lines!]!
}

type Query {
  orders(where: orders_where, filter: String, orderBy: [orders_order_by!], first: Int, skip: Int, after: String, search: String): [orders!]!
  orders_connection(where: …, filter: String, orderBy: [orders_connection_order_by!], first: Int, after: String): orders_connection!
  orders_by_key(id: Int!): orders
  node(id: ID!): Node
}
```

Relations are those [declared for the REST endpoints](./endpoints.md#querying-and-writing-rows) (`options.Relations`), to tables the caller may read.

### Conditions, order and pages

`where` is typed; `filter` takes the REST API's OData text. Given both, they are ANDed.

```graphql
{
  orders(
    where: { status: { eq: "open" }, total: { gte: 100 }, customer: { country: { in: ["FR", "DE"] } } }
    filter: "year(placed) eq 2024"
    orderBy: [{ total: DESC }, { customer: { name: ASC } }]
    first: 20
  ) { id total customer { name } }
}
```

- Each column has `eq`, `ne`, `gt`, `gte`, `lt`, `lte`, `in`, `nin` and `isNull`; text adds `contains`, `startsWith` and `endsWith`, which compare as the database does.
- `_and`, `_or` and `_not` combine conditions. A relation to many rows takes `any`, `all` or `none` (`lines: { any: { qty: { gt: 5 } } }`).
- Each item of `orderBy` names one column, or a related row's order.
- `first` and `skip` page a list, from 0 to `QueryLimits.MaxTop`. A list or connection without `first` reads `MaxTop` rows at most (1,000; a connection's `hasNextPage` says whether more follow); a related list without `first` reads every related row, all of a request's together held to `MaxExpandedRows`. `after` continues after a cursor, and `search` finds words in a [searchable](./endpoints.md#querying-and-writing-rows) table.

A relation's rows take the same arguments. One relation can be read twice, with other arguments, under two aliases:

```graphql
{ customers { name big: orders(where: { total: { gt: 1000 } }) { id } recent: orders(orderBy: [{ id: DESC }], first: 3) { id } } }
```

### Connections and global ids

Tables with a key the caller may read also have a **Relay connection** and **global ids**:

```graphql
query Page($after: String) {
  orders_connection(first: 20, after: $after, orderBy: [{ placed: DESC }]) {
    totalCount
    pageInfo { hasNextPage endCursor }
    edges { cursor node { nodeId id total } }
  }
}
```

Pages go forward (`first`, `after`), on the key's cursors; `hasPreviousPage` is always false. A connection is ordered by the table's own columns, and its key after them.

`nodeId` is a row's global id: opaque, stable, unique across the endpoint's tables. `node(id:)` reads any row by it, as the caller: an id of a table outside their schema, or of a row their policies hide, gives `null`. Each table keeps its own `id` column; the global id is `nodeId`, never `id` (see [Clients](#clients)).

### Writes

For each table, the mutations the caller may make:

| Field | Does | Returns |
|-------|------|---------|
| `insert_orders(rows: [orders_insert!]!)` | Inserts rows, related rows nested under their relations | `affected`, `rows`, `edges` |
| `save_orders(rows: [orders_insert!]!)` | Inserts rows, or updates those whose key exists | `affected`, `rows`, `edges` |
| `update_orders(where:, filter:, set: orders_set!)` | Updates the rows a condition matches | `affected`, `rows` |
| `delete_orders(where:, filter:)` | Deletes them | `affected`, `rows` **as they were** |
| `update_orders_by_key(id:, nodeId:, set:)` | Updates a row by its key or global id | `affected`, `row` |
| `delete_orders_by_key(id:, nodeId:)` | Deletes one | `affected`, `row` as it was |

- An update or delete by condition must be given one (`filter: "true"` for every row), so a missing `where` never means every row.
- Inputs hold the columns the caller may write; the policies' presets are set, not given.
- The rows a payload returns are read back in the same transaction, **as the caller**: a row they may write but not read is counted, not returned.
- `edges` lets Relay add new rows to a connection (`@appendEdge`).

**A mutation is one transaction.** Its fields run in order, functions among them; if any fails, nothing is written. The response then has no `data`, and every field that had succeeded has an error with code `ROLLED_BACK`. Every table a mutation writes is authorized before anything is written. Once it is committed, what follows (caches, subscriptions, the audit) cannot undo it: if one of those fails, the response keeps its `data` and adds an error saying so.

### Functions

[Declared functions](./endpoints.md#functions) the caller may run are fields: read-only ones on `Query`, those that declare `Writes` on `Mutation`. Each returns its rows as `[Any!]!`. In a mutation, a function runs in the mutation's transaction, all or none with its other fields.

### Subscriptions

Tables listed in [`RealtimeTables`](./changes.md#realtime-subscriptions) can be subscribed to, as **live queries**. A subscription takes a list field's arguments, sends its results, and runs again as the caller whenever a table it reads changes, sending new results only when they differ:

```graphql
subscription { orders(where: { status: { eq: "open" } }, orderBy: [{ id: ASC }]) { nodeId id status } }
```

Changes are heard as the REST [subscriptions](./changes.md#realtime-subscriptions) hear them, from other servers too. A run the caller is no longer allowed ends the subscription: it gets the run's errors, then `complete`.

## Scalars

| Column type | GraphQL | As |
|-------------|---------|----|
| `bool` | `Boolean` | |
| `byte` … `int` | `Int` | |
| `long`, `uint` | `Long` | a JSON number, or with `BigNumbersAsStrings` `BigInt`, a string |
| `ulong`, `Int128`, `BigInteger` | `BigInt` | a string |
| `float`, `double` | `Float` | |
| `decimal` | `Decimal` | a JSON number, or with `BigNumbersAsStrings` `BigDecimal`, a string |
| `string`, `char` | `String` | |
| `Guid` | `UUID` | `xxxxxxxx-xxxx-…` |
| `DateTimeOffset` | `DateTime` | `2024-05-17T09:30:00+02:00`; the offset (or `Z`) is required |
| `DateTime` | `LocalDateTime` | `2024-05-17T09:30:00`; text with an offset (a JavaScript `toISOString()`) is refused, never moved into the server's time zone |
| `DateOnly` | `Date` | `2024-05-17` |
| `TimeOnly` | `LocalTime` | `09:30:00` |
| `TimeSpan` | `TimeSpan` | ISO 8601, `PT1H30M` |
| `byte[]` | `ByteArray` | base64 |
| anything else (JSON, arrays, ranges, shapes) | `Any` | JSON |

The names are Hot Chocolate's, which Strawberry Shake maps to .NET types. JavaScript reads integers past 2<sup>53</sup> inexactly: set `BigNumbersAsStrings` if yours can be that large.

## Security

- **Each caller's own schema.** A caller's schema has only what they may use. An operation asking for anything else is refused whole, before it runs. Every table is authorized again when it is used, and its row filters, checks and presets apply then. Callers who may use the same share one schema, kept by its shape (`SchemaCacheSize`).
- **Introspection** is on by default (`Introspection`); the schema a caller sees is only their own. `GET …/graphql/schema.graphql` returns it as SDL, for code generators.
- **Limits** on every operation: `MaxDepth` (10), `MaxComplexity` (10,000) and `MaxRootFields` (20), plus the engine's own `QueryLimits`. An operation asking only for the schema, as code generators do, is not held to depth or complexity.
- **CSRF.** GET requests, and POSTs a form could send, need a header no form can set: `GraphQL-Require-Preflight` (or Apollo's `Apollo-Require-Preflight` or `X-Apollo-Operation-Name`). JSON POSTs need none. Batches of operations are refused.
- **Sockets.** A browser cannot set headers on a WebSocket, so the caller signs in on `connection_init`. A socket opened by a signed-in request is that caller's, and a browser can open one signed in with a [ticket](./authentication.md#streams-and-sockets-tickets) (`wss://…/graphql?ticket=…`). Otherwise the payload's `Authorization` is checked as the request's would be: an API key, a built-in account's token, then [your sign-in methods](./authentication.md#your-own-sign-in); or by your own `SignIn` hook. A token that is not accepted, a `SignIn` hook returning null, or a signed-in caller refused (no tenant where one is required, another tenant's datasource) has the socket closed with 4403. A socket's caller is checked before each operation: a tenant moved to another datasource since is refused each time, and one moved to another schema follows it when the URL names none. A message longer than `MaxSocketMessageBytes` (4 MiB) closes the socket with 1009. A socket opened from a page of another origin is refused unless listed in `AllowedOrigins`. A socket closes (4401) when its caller's session ends: their token's `exp`, or `MaxSessionAge`.
- **Errors** carry `extensions.code`: `FORBIDDEN`, `UNAUTHENTICATED`, `BAD_USER_INPUT`, `NOT_FOUND`, `CONFLICT`, a database error's class (`UNIQUE_VIOLATION`, `DEADLOCK`, …), or `INTERNAL_SERVER_ERROR`. A database's own message, and the exception behind an error, are shown only with `IncludeErrorDetails` (Development by default). A failed or refused field is audited as a failed REST request is.

### Code generation and roles

Clients are generated from a schema, and each role's is different: an operation must fit the schema of the role that runs it, or it is refused (422). Generate each role's client from that role's schema: sign in as the role and fetch `…/graphql/schema.graphql`, or introspect.

## Clients

### Apollo Client

```ts
const http = new HttpLink({ uri: "/api/dm/d/main/graphql" });
const ws = new GraphQLWsLink(createClient({ url: "wss://app.example.com/api/dm/d/main/graphql", connectionParams: () => ({ Authorization: `Bearer ${token()}` }) }));
const link = split(({ query }) => getMainDefinition(query).operation === "subscription", ws, http);
const client = new ApolloClient({
  link,
  cache: new InMemoryCache({ dataIdFromObject: o => (o.nodeId as string | undefined) ?? undefined }),
});
```

Rows of views have no `nodeId`; Apollo stores them under their parent, as `dataIdFromObject` returns `undefined`.

### Relay

```json
{ "src": "./src", "schema": "./schema.graphql", "language": "typescript", "schemaConfig": { "nodeInterfaceIdField": "nodeId" } }
```

With `nodeInterfaceIdField`, connections (`@connection`), refetchable fragments (`@refetchable`, through `node(id:)`) and `@appendEdge` work as Relay expects. Use `graphql-ws` in the network layer for subscriptions.

### Strawberry Shake

```json
{
  "schema": "schema.graphql",
  "documents": "**/*.graphql",
  "extensions": { "strawberryShake": { "name": "AppClient", "url": "https://app.example.com/api/dm/d/main/graphql", "transportProfiles": [{ "default": "Http", "subscription": "WebSocket" }] } }
}
```

Tell it that rows are identified by `nodeId`, in a `schema.extensions.graphql`. Its default is a field named `id`, which a view's rows need not have unique:

```graphql
extend type orders @key(fields: "nodeId")
```

The three are checked against DapperMatic's schema on every change (`tests/graphql-clients`).

## Options

```csharp
builder.Services.AddDapperMaticGraphQL(o =>
{
    o.Introspection = true;
    o.MaxDepth = 10;
    o.MaxComplexity = 10_000;
    o.MaxRootFields = 20;
    o.SchemaCacheSize = 256;
    o.BigNumbersAsStrings = false;
    o.AllowedOrigins.Add("https://admin.example.com");
    o.MaxSubscriptionsPerSocket = 20;
    o.MaxSocketMessageBytes = 4 * 1024 * 1024;
    o.ConnectionInitTimeout = TimeSpan.FromSeconds(3);
    o.SignIn = async (payload, http) => await myTokens.ValidateAsync(payload?.GetProperty("token").GetString());
});
```

## Not yet

Backward paging (`last`, `before`); aggregates; typed function results; persisted queries; batches of operations; federation; ETags on GET responses (the REST endpoints have them).
