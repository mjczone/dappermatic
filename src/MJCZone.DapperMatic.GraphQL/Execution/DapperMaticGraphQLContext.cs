// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using GraphQLParser.AST;
using MJCZone.DapperMatic.AspNetCore;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.GraphQL.Schema;

namespace MJCZone.DapperMatic.GraphQL.Execution;

/// <summary>
/// What a GraphQL request runs as (its user context): the caller's session, and the operation the endpoint was asked
/// for, with its datasource and schema held to the caller's tenant. Each execution gets one of its own, with its schema
/// and its writes; a socket's subscriptions never share theirs.
/// </summary>
internal sealed class DapperMaticGraphQLContext : Dictionary<string, object?>
{
    private readonly object _lock = new();
    private readonly string? _namedSchema;

    // The session and the operation held to it, changed together: an operation never runs as one session held to
    // another's tenant.
    private volatile Caller _caller;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticGraphQLContext"/> class.
    /// </summary>
    /// <param name="session">The caller's session.</param>
    /// <param name="operation">The operation, held to the caller's tenant.</param>
    /// <param name="namedSchema">The schema the endpoint's URL names, or null: what the operation is held again from.</param>
    public DapperMaticGraphQLContext(DapperMaticCallerSession session, DapperMaticOperation operation, string? namedSchema)
    {
        _caller = new Caller(session, operation);
        _namedSchema = namedSchema;
    }

    /// <summary>Gets the caller's session.</summary>
    public DapperMaticCallerSession Session => _caller.Session;

    /// <summary>Gets the operation the endpoint was asked for: <c>graphql</c>, on its datasource and schema.</summary>
    public DapperMaticOperation Operation => _caller.Operation;

    /// <summary>Gets the datasource.</summary>
    public string DatasourceId => Operation.DatasourceId ?? throw new InvalidOperationException("The GraphQL endpoint names no datasource.");

    /// <summary>Gets or sets the execution's schema, and what its types stand for.</summary>
    public GraphQLModel? Model { get; set; }

    /// <summary>Gets or sets the execution's writes, when it is a mutation.</summary>
    public DapperMaticService.WriteSession? Writes { get; set; }

    /// <summary>
    /// Gets the keys the execution's relation fields' rows are put under in their rows, by field: chosen when the
    /// query is built, read by the fields' resolvers. Lock it to change it.
    /// </summary>
    public Dictionary<GraphQLField, string> ExpandKeys { get; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Makes the context of one execution: the same caller and operation, nothing of another execution's.
    /// </summary>
    /// <returns>The context.</returns>
    public DapperMaticGraphQLContext ForExecution()
    {
        var (session, operation) = _caller;
        return new(session, operation, _namedSchema);
    }

    /// <summary>
    /// Takes the caller's session as it is now (a socket's, checked before each use), the operation held to it again
    /// as the URL named it: with no schema there, it follows the tenant's. A session the operation cannot be held to
    /// (the URL names a schema no longer the tenant's) is refused, and the one before stays: each later operation is
    /// checked, and refused, again.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <exception cref="UnauthorizedAccessException">The operation is not the session's tenant's.</exception>
    public void Use(DapperMaticCallerSession session)
    {
        lock (_lock)
        {
            if (!ReferenceEquals(session, _caller.Session))
            {
                var held = session.Builder.Hold(session, _caller.Operation with { SchemaName = _namedSchema });
                _caller = new Caller(session, held);
            }
        }
    }

#pragma warning disable SA1313
    private sealed record Caller(DapperMaticCallerSession Session, DapperMaticOperation Operation);
#pragma warning restore SA1313
}
