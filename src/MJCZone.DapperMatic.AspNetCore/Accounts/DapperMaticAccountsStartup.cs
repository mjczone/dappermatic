// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MJCZone.DapperMatic.Migrations;
using MJCZone.DapperMatic.Models;

namespace MJCZone.DapperMatic.AspNetCore.Accounts;

/// <summary>
/// Readies the built-in accounts before the application serves requests: their tables made (DapperMatic's migrator),
/// Data Protection's keys checked to be kept encrypted, and the first administrator invited, or a one-time setup code
/// written to the log while no account exists.
/// </summary>
internal sealed class DapperMaticAccountsStartup(IServiceProvider services, ILogger<DapperMaticAccountsStartup> logger) : IHostedService
{
    /// <summary>The setting the setup code's hash is kept as.</summary>
    internal const string SetupCode = "setup_code";

    private static readonly Type[] Tables =
    [
        typeof(DapperMaticUser),
        typeof(DapperMaticUserRole),
        typeof(DapperMaticMembership),
        typeof(DapperMaticInvitation),
        typeof(DapperMaticUserSession),
        typeof(DapperMaticAccountSetting),
        typeof(DapperMaticDataProtectionKey),
    ];

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = services.GetRequiredService<IOptions<DapperMaticOptions>>().Value;
        var db = services.GetRequiredService<DapperMaticAccountDb>();
        using (var connection = await db.OpenAsync(cancellationToken).ConfigureAwait(false))
        {
            var migrator = new DmMigrator(new DmMigrationOptions { SchemaName = db.Schema, TableStateTableName = "dm_accounts_state" });
            foreach (var type in Tables)
            {
                var table = DmTableFactory.GetTable(type);
                migrator.AddTable(db.Schema is { Length: > 0 } schema ? DmMigrator.CopyToSchema(table, schema) : table);
            }

            await migrator.MigrateAsync(connection, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        if (options.Accounts.KeepDataProtectionKeys)
        {
            if (string.IsNullOrWhiteSpace(options.ConnectionStringEncryptionKey))
            {
                throw new InvalidOperationException(
                    "DapperMatic keeps the application's Data Protection keys encrypted, with ConnectionStringEncryptionKey: set it (CryptoUtils.GenerateEncryptionKey()), or keep the keys yourself (a.KeepDataProtectionKeys = false; services.AddDataProtection().PersistKeysTo…)."
                );
            }

            logger.LogInformation("DapperMatic keeps this application's Data Protection keys, encrypted, in dm_data_protection_keys (Accounts.KeepDataProtectionKeys).");
        }

        // JWT settings are checked now, not at the first sign-in.
        if (services.GetRequiredService<DapperMaticJwt>().Enabled)
        {
            logger.LogInformation("DapperMatic issues the accounts' access tokens as JWTs; their keys are published at auth/jwks.json.");
        }

        await FirstAdministratorAsync(options.Accounts, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Hashes a code kept only as a hash.</summary>
    /// <param name="code">The code.</param>
    /// <returns>Its SHA-256, as hexadecimal.</returns>
    internal static string Hash(string code) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    // The first administrator: invited when configured (unless their account or invitation exists); otherwise, while
    // no account exists, a setup code. A code made by another instance a moment ago is left to that instance's log.
    private async Task FirstAdministratorAsync(DapperMaticAccountOptions accounts, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<DapperMaticAccountStore>();
        var first = accounts.FirstAdmin ?? FromConfiguration();
        if (first?.Email is { Length: > 0 } email)
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<DapperMaticUser>>();
            var normalized = users.NormalizeEmail(email);
            if (await users.FindByEmailAsync(email).ConfigureAwait(false) is null
                && !(await store.InvitationsAsync(string.Empty, cancellationToken).ConfigureAwait(false)).Exists(i => i.NormalizedEmail == normalized && i.ExpiresAt > DateTimeOffset.UtcNow))
            {
                await scope.ServiceProvider.GetRequiredService<DapperMaticInvitations>()
                    .InviteAsync(email, tenant: null, first.Roles, invitedBy: "configuration", cancellationToken)
                    .ConfigureAwait(false);
                logger.LogInformation("DapperMatic invited the first administrator, {Email}.", email);
            }

            return;
        }

        if (await store.AnyUserAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var kept = await store.SettingAsync(SetupCode, cancellationToken).ConfigureAwait(false);
        if (kept?.Split(':', 2) is [_, var at] && DateTimeOffset.TryParse(at, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var made)
            && DateTimeOffset.UtcNow - made < TimeSpan.FromMinutes(1))
        {
            return;
        }

        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
        var value = $"{Hash(code)}:{DateTimeOffset.UtcNow:O}";
        if (kept is not null)
        {
            await store.TakeSettingAsync(SetupCode, kept, cancellationToken).ConfigureAwait(false);
        }

        if (await store.AddSettingAsync(SetupCode, value, cancellationToken).ConfigureAwait(false))
        {
            logger.LogWarning(
                "DapperMatic has no accounts yet. Create the first administrator with POST {{BasePath}}/auth/setup {{ code, email, password }}, code {Code} (good until an account exists).",
                code
            );
        }
    }

    private DapperMaticFirstAdmin? FromConfiguration()
    {
        var section = services.GetService<IConfiguration>()?.GetSection("DapperMatic:Accounts:FirstAdmin");
        if (section?["Email"] is not { Length: > 0 } email)
        {
            return null;
        }

        var admin = new DapperMaticFirstAdmin { Email = email };
        if (section.GetSection("Roles").Get<string[]>() is { Length: > 0 } roles)
        {
            admin.Roles.Clear();
            admin.Roles.AddRange(roles);
        }

        return admin;
    }
}
