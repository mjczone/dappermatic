// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MJCZone.DapperMatic.AspNetCore.Authentication;

/// <summary>The names of DapperMatic's API key sign-in method.</summary>
public static class DapperMaticApiKeyDefaults
{
    /// <summary>The sign-in method (authentication scheme) of API keys, which an application's own endpoints can use too.</summary>
    public const string Scheme = "DapperMatic.ApiKey";
}

/// <summary>
/// Signs callers in by API key: <c>Authorization: Bearer {prefix}_…</c>, or the key header. A bearer token that is not
/// a key (another method's) is left to that method.
/// </summary>
internal sealed class DapperMaticApiKeyHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly DapperMaticApiKeyService _keys;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticApiKeyHandler"/> class.
    /// </summary>
    /// <param name="options">The scheme's options.</param>
    /// <param name="logger">The logger factory.</param>
    /// <param name="encoder">The URL encoder.</param>
    /// <param name="keys">The keys.</param>
    public DapperMaticApiKeyHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, DapperMaticApiKeyService keys)
        : base(options, logger, encoder)
    {
        _keys = keys;
    }

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var settings = _keys.Settings;
        var authorization = Request.Headers.Authorization.ToString();
        string? presented = null;
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) && authorization[7..].Trim() is var token && token.StartsWith(settings.Prefix + "_", StringComparison.Ordinal))
        {
            presented = token;
        }
        else if (Request.Headers[settings.HeaderName].ToString().Trim() is { Length: > 0 } header)
        {
            presented = header;
        }

        return presented is null ? Task.FromResult(AuthenticateResult.NoResult()) : AuthenticateAsync(presented);
    }

    // The key, its owner just checked: the session's check need not look the owner up again in this request.
    private async Task<AuthenticateResult> AuthenticateAsync(string presented)
    {
        var result = await _keys.AuthenticateAsync(presented, Context.RequestServices, Scheme.Name, Context.RequestAborted).ConfigureAwait(false);
        if (result.Succeeded && result.Principal!.FindFirst(DapperMaticClaims.Key)?.Value is { } id)
        {
            Context.Items[DapperMaticApiKeyCheck.Checked] = id;
        }

        return result;
    }
}

/// <summary>
/// Keeps when keys were last used: once a minute, what was recorded since.
/// </summary>
internal sealed class DapperMaticKeyUseWorker : BackgroundService
{
    private readonly DapperMaticApiKeyService _keys;
    private readonly ILogger<DapperMaticKeyUseWorker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperMaticKeyUseWorker"/> class.
    /// </summary>
    /// <param name="keys">The keys.</param>
    /// <param name="logger">The logger.</param>
    public DapperMaticKeyUseWorker(DapperMaticApiKeyService keys, ILogger<DapperMaticKeyUseWorker> logger)
    {
        _keys = keys;
        _logger = logger;
    }

    /// <summary>
    /// Keeps what was recorded now: when the application stops, and in tests.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    internal async Task FlushAsync(CancellationToken cancellationToken)
    {
        var used = _keys.TakeUsed();
        if (used.Count == 0)
        {
            return;
        }

        try
        {
            await _keys.Repository.TouchAsync(used, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "DapperMatic could not keep when {Count} API keys were last used.", used.Count);
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await FlushAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Stopping: what is left is kept below.
        }

        await FlushAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
