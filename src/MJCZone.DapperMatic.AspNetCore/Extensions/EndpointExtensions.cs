using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MJCZone.DapperMatic.AspNetCore.Auditing;
using MJCZone.DapperMatic.AspNetCore.Validation;

namespace MJCZone.DapperMatic.AspNetCore.Extensions;

/// <summary>
/// Extension methods for configuring endpoint conventions.
/// </summary>
internal static class EndpointExtensions
{
    /// <summary>
    /// Maps a DapperMatic route group with the specified base path and prefix.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="basePath">The base path for the API endpoints. Defaults to "/api/dm".</param>
    /// <param name="prefix">The prefix for the specific resource (e.g., "datasources", "tables").</param>
    /// <param name="tag">The OpenAPI tag to associate with the group.</param>
    /// <returns>The configured route group.</returns>
    public static RouteGroupBuilder MapDapperMaticEndpointGroup(
        this IEndpointRouteBuilder endpoints,
        string? basePath,
        [StringSyntax("Route")] string prefix,
        string tag
    )
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        basePath ??= "/api/dm";
        return endpoints
            .MapGroup($"/{basePath.Trim('/')}/{prefix.TrimStart('/')}")
            .WithDapperMaticConventions(tag);
    }

    /// <summary>
    /// Adds DapperMatic conventions to the specified route group.
    /// </summary>
    /// <param name="group">The route group to configure.</param>
    /// <param name="tag">The OpenAPI tag to associate with the group.</param>
    /// <returns>The configured route group.</returns>
    private static RouteGroupBuilder WithDapperMaticConventions(
        this RouteGroupBuilder group,
        string tag
    )
    {
        return group.WithTags(tag).AddEndpointFilter<DapperMaticExceptionFilter>().WithOpenApi();
    }

    /// <summary>
    /// Exception filter to handle common exceptions and map them to appropriate HTTP results.
    /// </summary>
    public class DapperMaticExceptionFilter : IEndpointFilter
    {
        /// <inheritdoc/>
        public async ValueTask<object?> InvokeAsync(
            EndpointFilterInvocationContext context,
            EndpointFilterDelegate next
        )
        {
            try
            {
                return await next(context).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var operationContext =
                    context.HttpContext.RequestServices.GetService<IOperationContext>();
                var auditLogger =
                    context.HttpContext.RequestServices.GetService<IDapperMaticAuditLogger>();
                if (auditLogger != null && operationContext != null)
                {
                    await auditLogger
                        .LogOperationAsync(
                            operationContext.ToAuditEvent(success: false, message: ex.Message)
                        )
                        .ConfigureAwait(false);
                }
                return ErrorHandler.HandleError(ex);
            }
        }
    }
}
