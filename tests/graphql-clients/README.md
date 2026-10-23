# GraphQL client compatibility

`schema.graphql` is the schema DapperMatic's GraphQL endpoint builds for a fixed caller model (customers, their
orders, a view, two functions; every kind of column). `GraphQLSchemaTests` checks it against the builder; run that
test with `DM_UPDATE_GOLDEN=1` to write it after a deliberate change, and review the diff: it is what clients see.

Each folder generates a client from it with `operations.graphql` (Relay's operations are in `relay/src`, in Relay's own
naming), as `.github/workflows/graphql-clients.yml` does:

| Folder | Client | Run |
|--------|--------|-----|
| `codegen` | Apollo Client's types (graphql-codegen, `strictScalars`, then `tsc --strict`) | `npm ci && npm run check` |
| `relay` | Relay (`relay-compiler`, `nodeInterfaceIdField: "nodeId"`, a `@connection`, a `@refetchable` fragment, `@appendEdge`) | `npm ci && npm run check` |
| `strawberry` | Strawberry Shake (generated on build; rows keyed by `nodeId`) | `dotnet build` |
