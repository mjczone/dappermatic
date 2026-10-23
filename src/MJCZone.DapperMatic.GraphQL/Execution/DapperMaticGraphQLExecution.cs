// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using GraphQL;
using GraphQL.DI;
using GraphQL.Validation;
using GraphQL.Validation.Complexity;
using GraphQL.Validation.Rules.Custom;
using GraphQLParser.AST;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.AspNetCore.Security;
using MJCZone.DapperMatic.AspNetCore.Services;
using MJCZone.DapperMatic.GraphQL.Schema;

namespace MJCZone.DapperMatic.GraphQL.Execution;

/// <summary>
/// Prepares each execution of DapperMatic's endpoint, over HTTP and sockets alike: a context of its own, the caller's
/// session checked (a socket's may have ended), the caller's schema (from their model, cached by its shape), and the
/// limits on what an operation may ask for. Executions of an application's own schemas are left as they are.
/// </summary>
internal sealed class DapperMaticGraphQLExecution : IConfigureExecution
{
    private readonly GraphQLSchemaCache _schemas;
    private readonly DapperMaticGraphQLOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticGraphQLExecution"/> class.
    /// </summary>
    /// <param name="schemas">The schemas built.</param>
    /// <param name="options">The GraphQL options.</param>
    public DapperMaticGraphQLExecution(GraphQLSchemaCache schemas, IOptions<DapperMaticGraphQLOptions> options)
    {
        _schemas = schemas;
        _options = options.Value;
    }

    /// <inheritdoc />
    public float SortOrder => 50;

    /// <inheritdoc />
    public async Task<ExecutionResult> ExecuteAsync(ExecutionOptions options, ExecutionDelegate next)
    {
        if (options.UserContext is not DapperMaticGraphQLContext shared)
        {
            return await next(options).ConfigureAwait(false);
        }

        DapperMaticGraphQLContext request;
        try
        {
            // A socket's session is checked before each operation; a request's was built for it. A refusal is the
            // signed-in caller's.
            DapperMaticCallerSession.Current = shared.Session;
            shared.Use(await shared.Session.CheckAsync(options.CancellationToken).ConfigureAwait(false));
            request = shared.ForExecution();
            options.UserContext = request;
            DapperMaticCallerSession.Current = request.Session;
            var service = options.RequestServices!.GetRequiredService<IDapperMaticService>() as DapperMaticService
                ?? throw new InvalidOperationException("DapperMatic's GraphQL endpoint needs DapperMatic's own service.");
            var model = await service.DescribeForCallerAsync(request.Operation, request.DatasourceId, request.Operation.SchemaName, withFunctions: true, options.CancellationToken)
                .ConfigureAwait(false);
            request.Model = _schemas.Get(model);
        }
        catch (Exception ex)
        {
            await GraphQLErrors.AuditAsync(options.RequestServices!, shared.Operation, ex).ConfigureAwait(false);
            return new ExecutionResult { Errors = [GraphQLErrors.Of(ex, options.RequestServices!)] };
        }

        options.Schema = request.Model.Schema;
        options.ThrowOnUnhandledException = false;
        IValidationRule[] limits =
        [
            new ComplexityValidationRule(
                new ComplexityOptions
                {
                    MaxDepth = _options.MaxDepth,
                    MaxComplexity = _options.MaxComplexity,

                    // Asking for the schema only (as code generators do) is not held to the limits.
                    ValidateComplexityDelegate = context =>
                    {
                        if (IntrospectionOnly(context.ValidationContext.Operation))
                        {
                            context.Error = null;
                        }

                        return Task.CompletedTask;
                    },
                }
            ),
            new RootFieldsValidationRule(_options.MaxRootFields),
            .. _options.Introspection ? Array.Empty<IValidationRule>() : [NoIntrospectionValidationRule.Instance],
        ];
        options.ValidationRules = [.. options.ValidationRules ?? DocumentValidator.CoreRules, .. limits];
        options.CachedDocumentValidationRules = [.. options.CachedDocumentValidationRules ?? [], .. limits];
        return await next(options).ConfigureAwait(false);
    }

    /// <summary>Tells whether an operation asks only for the schema (<c>__schema</c>, <c>__type</c>).</summary>
    /// <param name="operation">The operation.</param>
    /// <returns>True if it does.</returns>
    internal static bool IntrospectionOnly(GraphQLOperationDefinition? operation) =>
        operation?.SelectionSet.Selections is { Count: > 0 } selections
        && selections.All(s => s is GraphQLField field && field.Name.StringValue.StartsWith("__", StringComparison.Ordinal));
}

/// <summary>
/// Limits how many root fields an operation has: each runs a query or write of its own, so aliases could otherwise fan
/// one request out into many.
/// </summary>
/// <param name="max">The most root fields.</param>
internal sealed class RootFieldsValidationRule(int max) : ValidationRuleBase
{
    /// <inheritdoc />
    public override ValueTask<INodeVisitor?> GetPreNodeVisitorAsync(ValidationContext context)
    {
        if (context.Operation?.SelectionSet.Selections is { } selections && Count(context, selections, []) > max)
        {
            context.ReportError(new ValidationError(context.Document.Source, "5.3.1", $"An operation has at most {max} root fields.", context.Operation));
        }

        return default;
    }

    // Root fields, through the fragments they are spread from.
    private static int Count(ValidationContext context, IEnumerable<ASTNode> selections, HashSet<string> spread) =>
        selections.Sum(s => s switch
        {
            GraphQLField => 1,
            GraphQLInlineFragment inline => Count(context, inline.SelectionSet.Selections, spread),
            GraphQLFragmentSpread fragment when spread.Add(fragment.FragmentName.Name.StringValue)
                && context.Document.Definitions.OfType<GraphQLFragmentDefinition>().FirstOrDefault(d => d.FragmentName.Name.StringValue == fragment.FragmentName.Name.StringValue) is { } definition
                => Count(context, definition.SelectionSet.Selections, spread),
            _ => 0,
        });
}
