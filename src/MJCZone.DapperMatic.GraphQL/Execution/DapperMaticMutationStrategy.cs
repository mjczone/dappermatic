// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using GraphQL;
using GraphQL.Execution;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Models;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.GraphQL.Schema;
using GraphQLExecutionContext = GraphQL.Execution.ExecutionContext;

namespace MJCZone.DapperMatic.GraphQL.Execution;

/// <summary>
/// Runs a mutation of DapperMatic's endpoint as one transaction, as batches run: every field's table authorized and
/// opened first (MySQL commits on DDL, and describing a table inside the transaction is refused by SQL Server), the
/// fields one after another, then committed if none failed. If any failed, nothing is written: the response has no
/// data, and each field that had succeeded says it was rolled back. Mutations of an application's own schemas run as
/// usual.
/// </summary>
internal sealed class DapperMaticMutationStrategy : SerialExecutionStrategy
{
    /// <inheritdoc />
    public override async Task<ExecutionResult> ExecuteAsync(GraphQLExecutionContext context)
    {
        if (context.UserContext is not DapperMaticGraphQLContext request || request.Model is null)
        {
            return await base.ExecuteAsync(context).ConfigureAwait(false);
        }

        var services = context.RequestServices!;
        var service = services.GetRequiredService<IDapperMaticService>() as DapperMaticService
            ?? throw new InvalidOperationException("DapperMatic's GraphQL endpoint needs DapperMatic's own service.");
        var fields = CollectFieldsFrom(context, context.Schema.Mutation!, context.Operation.SelectionSet, null);
        using var session = await service.BeginWritesAsync(request.Operation, request.DatasourceId).ConfigureAwait(false);
        try
        {
            foreach (var (_, (_, definition)) in fields)
            {
                if (definition.Metadata.TryGetValue(GraphQLBinding.Kind, out var kind) && kind is GraphQLFieldKind write && Write(write) is { } name)
                {
                    await session.OpenAsync(definition.GetMetadata<GraphQLTable>(GraphQLBinding.Table)!.Table.Name, name, context.CancellationToken).ConfigureAwait(false);
                }
                else if (kind is GraphQLFieldKind.Function)
                {
                    await session.OpenFunctionAsync(definition.GetMetadata<DapperMaticFunction>(GraphQLBinding.Function)!.Name, context.CancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            await GraphQLErrors.AuditAsync(services, request.Operation, ex).ConfigureAwait(false);
            return new ExecutionResult { Executed = true, Errors = [GraphQLErrors.Of(ex, services)] };
        }

        session.Begin();
        request.Writes = session;
        ExecutionResult result;
        try
        {
            // A field's error is in the context's errors; the result gets them only after this returns.
            result = await base.ExecuteAsync(context).ConfigureAwait(false);
            if (context.Errors.Count == 0)
            {
                session.Commit();
            }
        }
        catch (Exception ex)
        {
            session.Rollback();
            await GraphQLErrors.AuditAsync(services, request.Operation, ex).ConfigureAwait(false);
            return new ExecutionResult { Executed = true, Errors = [GraphQLErrors.Of(ex, services)] };
        }
        finally
        {
            request.Writes = null;
        }

        if (context.Errors.Count == 0)
        {
            // Written: what follows a write failing (a cache, the audit) leaves the rows written and the payload read,
            // and is said so.
            try
            {
                await session.CommittedAsync(context.CancellationToken).ConfigureAwait(false);
            }
            catch (AggregateException ex)
            {
                var error = GraphQLErrors.Of(ex.InnerExceptions[0], services);
                (result.Errors ??= []).Add(new ExecutionError($"Written; then this failed: {error.Message}") { Code = error.Code });
            }

            return result;
        }

        // All or none: what had succeeded is undone (functions too: they ran in the transaction), and said to be.
        session.Rollback();
        var failed = context.Errors.Select(e => e.Path?.FirstOrDefault()?.ToString()).ToHashSet(StringComparer.Ordinal);
        foreach (var key in fields.Keys.Where(k => !failed.Contains(k)))
        {
            context.Errors.Add(new ExecutionError("Rolled back: another field of the mutation failed, and nothing was written.") { Code = GraphQLErrors.RolledBack, Path = [key] });
        }

        result.Data = null;
        return result;
    }

    private static string? Write(GraphQLFieldKind kind) =>
        kind switch
        {
            GraphQLFieldKind.Insert => "insert-rows",
            GraphQLFieldKind.Save => "save-rows",
            GraphQLFieldKind.Update or GraphQLFieldKind.UpdateByKey => "update-rows",
            GraphQLFieldKind.Delete or GraphQLFieldKind.DeleteByKey => "delete-rows",
            _ => null,
        };
}
