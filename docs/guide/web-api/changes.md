# Webhooks and Realtime

DapperMatic can tell other systems when rows change, and push new query results to browsers and apps that stay connected. Both hear the changes made **through DapperMatic**:

- the row endpoints (`rows`, `rows/save`, `rows/update`, `rows/delete`) and batches;
- functions, for the tables they declare in `Writes`;
- application code that writes through `DapperMaticCallerDb`.

A change made by another application or a SQL script is not heard.

## How changes are noted

Each change to a table that a webhook or subscription hears is noted in the datasource's own `dm_changes` table, **in the same transaction as the write**. A write that rolls back (a dry run, a refused check, a failed batch) notes nothing. A committed write is never lost, even if the server stops right after.

A change names the table, the action (`insert`, `update`, `delete`, or `write` for functions), the **keys** of the rows, and their tenant. It never includes other columns. Keys are read as the write runs: PostgreSQL returns them from the statement (`RETURNING`), and SQL Server, MySQL and SQLite read them first, under locks, in the same transaction. A write of many rows is noted once per tenant, in chunks of 500 keys.

DapperMatic creates `dm_changes` and `dm_webhook_state` in each datasource that needs them, the first time. Put them in a schema of their own with `options.ChangeTablesSchema = "dm"`. If the application's database user may not create tables, create them ahead of time with a user who may. Changes are kept for `WebhookDelivery.Retention` (7 days), then pruned by the webhook worker of each server whose `WebhookDelivery.Enabled` is on (the default), whether webhooks or only subscriptions hear them. The tables are DapperMatic's own: no endpoint serves them (see [Tables no policy reaches](./security#tables-no-policy-reaches)).

## Webhooks

```csharp
builder.Services.Configure<DapperMaticOptions>(options =>
{
    options.Webhooks.Add(new DapperMaticWebhook
    {
        Name = "orders",
        Url = "https://hooks.example.com/orders",
        Secret = builder.Configuration["Hooks:OrdersSecret"],
        Tables = { "orders", "order_lines" },        // patterns allowed; empty = every table
        Actions = DapperMaticWebhookActions.Insert | DapperMaticWebhookActions.Update,
    });
});
```

Each change is posted as one message, in order:

```http
POST /orders HTTP/1.1
Content-Type: application/json
X-DapperMatic-Delivery: 1842
X-DapperMatic-Event: orders.update
X-DapperMatic-Timestamp: 1792656000
X-DapperMatic-Signature: sha256=5d1c…

{ "id": 1842, "webhook": "orders", "datasource": "main", "schema": "public", "table": "orders",
  "action": "update", "tenant": "17", "keys": [1001, 1002], "count": 2, "at": "2026-10-22T09:30:00Z" }
```

`keys` lists the key values. For a table with a composite key, it lists objects (`[{"order_id":1,"line":2}]`). To check a message came from you, compute `HMAC-SHA256(secret, "{X-DapperMatic-Timestamp}.{body}")` in hex and compare it with the signature. Reject old timestamps.

**Delivery.** A background worker sends each webhook what is due to it every `WebhookDelivery.PollInterval` (2 s):

- Messages go in order.
- A message that fails (no 2xx, or no answer within 10 s) is retried after 2, 4, 8, … seconds, up to an hour between tries. Nothing after it is sent first.
- After `MaxAttempts` (12) tries, the message is dropped and logged, and the next one is sent.
- Servers sharing a datasource take turns through a lease, so each message is sent once.
- Turn the worker off with `WebhookDelivery.Enabled = false` on servers that should only take requests.

A new webhook hears the changes made after it is first seen.

### Webhooks kept through the API

Administrators of access (`ManageAccess`) keep webhooks at runtime, beside policies:

| Method | Path | |
|--------|------|---|
| `GET` | `/api/dm/access/webhooks` | the webhooks the caller manages, secrets masked |
| `GET` | `/api/dm/access/webhooks/{name}` | one |
| `PUT` | `/api/dm/access/webhooks/{name}` | add or replace; a secret left out keeps the one kept |
| `DELETE` | `/api/dm/access/webhooks/{name}` | remove |
| `GET` | `/api/dm/access/webhooks/{name}/status` | per datasource: delivered, dropped, attempts, next try, last error |

Every path takes `?tenant=` for a tenant's webhooks. Secrets are never returned (they read `********`). They are encrypted at rest with `ConnectionStringEncryptionKey` when one is set. An access import leaves webhooks as they are.

**A tenant's webhooks.** A tenant's administrator (`Allow(ManageAccess, …).WithinOwnTenant()`) keeps webhooks for their tenant only:

- The webhook must name its datasource and its tables, each one a table the administrator reads **whole**: every row of
  the tenant's and every column. A webhook tells of each of the tenant's rows that changes, with its key, so a table the
  administrator reads only some rows of (a filter beyond the tenant's) or some columns of is refused (403).
- It is held to the tenant's datasource and schema.
- It **hears only the tenant's rows**. That means rows whose tenant column (`Tenancy.RowsMarkedBy(...)`) holds the tenant, or any table in the tenant's own schema or datasource.

Rows of no tenant, and other tenants' rows, are heard only by the application's webhooks.

**Internal addresses are refused.** A webhook kept through the API may not post to loopback, private, link-local (cloud metadata among them) or other internal addresses. The address is checked when the connection is made, so DNS tricks don't get around it. IPv6 addresses carrying an IPv4 one (NAT64 `64:ff9b::/96`, 6to4, IPv4-compatible) are judged by that address; Teredo and local NAT64 addresses are refused. Redirects are not followed, and no proxy is used, so the address checked is the one connected to. Webhooks given in code or configuration may post anywhere. Allow internal addresses for API webhooks with `WebhookDelivery.AllowPrivateAddresses = true`.

## Realtime subscriptions

List the tables and views callers may subscribe to:

```csharp
options.RealtimeTables.Add(new DapperMaticRealtimeTable { TableName = "orders" });
options.RealtimeTables.Add(new DapperMaticRealtimeTable { TableName = "customers" });
options.RealtimeTables.Add(new DapperMaticRealtimeTable { TableName = "order_totals", Reads = ["orders"] }); // a view
```

A client subscribes to a query with the same options as `/query`, and gets **Server-Sent Events**:

```js
const events = new EventSource("/api/dm/d/main/t/orders/subscribe?$filter=status eq 'open'&$orderby=placed desc&$expand=customer");
events.addEventListener("results", e => render(JSON.parse(e.data).result));
events.addEventListener("error", e => console.warn(e.data)); // the query no longer runs (the policies or the table changed)
```

- The first `results` event is the query's results. After that, the query is run again, as the same caller, whenever a table it reads changes. That includes the tables of the relations it expands, filters or orders by. A new `results` event is sent only when the results differ.
- Policies apply to every run. Each subscriber gets only what it may read, so a change to rows it cannot see sends it nothing.
- The query must read only listed tables, or the subscription is refused (400). A view must name the tables it reads (`Reads`).
- Changes close together are run once (`Realtime.Debounce`, 250 ms). Every subscription gets a `: ping` comment every `Realtime.Heartbeat` (15 s), so proxies keep it open. Pings keep their own time, whatever changes: a ping put off by a change would tell the subscriber that a row it cannot see changed.
- **Several servers.** A change made on this server is heard at once. A change made on another server is read from `dm_changes` every `Realtime.PollInterval` (1 s).
- **A subscription lasts as long as its caller's session.** Before each run, the policies and tenants kept through the API are read again when due (`AccessRefreshInterval`), so access taken away ends the subscription with an `error` event. So does the caller's token expiring (its `exp` claim), or `MaxSessionAge` (one hour by default) passing: the client opens it again, signed in afresh. `EventSource` reconnects on its own.

`EventSource` cannot send an `Authorization` header. Ask for a one-time [ticket](./authentication.md#streams-and-sockets-tickets) and open the stream with `?ticket=…`, or use cookie authentication, or a client library that sends headers (for example `@microsoft/fetch-event-source`).
