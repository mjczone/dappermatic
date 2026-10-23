// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using MJCZone.DapperMatic.AspNetCore.Models;

namespace MJCZone.DapperMatic.AspNetCore.Security;

/// <summary>
/// Checks a datasource added or changed through the API against <see cref="DapperMaticOptions.ConnectionPolicy"/>.
/// </summary>
internal static class ConnectionPolicyCheck
{
    private static readonly string[] HostKeys = ["server", "host", "datasource", "address", "addr", "networkaddress"];

    /// <summary>
    /// Refuses a provider and connection string the policy does not allow.
    /// </summary>
    /// <param name="options">The options, with the policy and the registered drivers.</param>
    /// <param name="provider">The provider name.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>
    /// The connection string to store: as given, except that a SQLite file's path is made absolute, relative to
    /// <see cref="DapperMaticConnectionPolicy.SqliteDirectory"/>.
    /// </returns>
    /// <exception cref="ArgumentException">The policy does not allow it.</exception>
    public static string Check(DapperMaticOptions options, string? provider, string? connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var policy = options.ConnectionPolicy;
        var providerType = DapperMaticProviders.Parse(provider);

        if (policy.AllowedProviders.Count > 0 && !policy.AllowedProviders.Any(p => DapperMaticProviders.Parse(p) == providerType))
        {
            throw new ArgumentException($"{providerType} datasources are not allowed here.", nameof(provider));
        }

        if (!options.ProviderFactories.ContainsKey(providerType))
        {
            throw new ArgumentException(DapperMaticProviders.NotRegisteredMessage(providerType), nameof(provider));
        }

        Dictionary<string, string> settings;
        DbConnectionStringBuilder builder;
        try
        {
            builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            settings = builder.Keys.Cast<string>().ToDictionary(Normalize, k => Convert.ToString(builder[k], CultureInfo.InvariantCulture) ?? string.Empty);
        }
        catch (ArgumentException)
        {
            throw new ArgumentException("The connection string is not well formed.", nameof(connectionString));
        }

        string? Setting(params string[] keys) => keys.Select(k => settings.GetValueOrDefault(k)).FirstOrDefault(v => v is not null);

        switch (providerType)
        {
            case DbProviderType.MySql:
                if (!policy.AllowLoadLocalInfile && IsTrue(Setting("allowloadlocalinfile")))
                {
                    throw new ArgumentException(
                        "AllowLoadLocalInfile lets the database server ask for files on this machine, and is not allowed here.",
                        nameof(connectionString)
                    );
                }

                break;

            case DbProviderType.SqlServer:
                if (Setting("attachdbfilename", "initialfilename", "extendedproperties") is { Length: > 0 })
                {
                    throw new ArgumentException("Attaching a database file is not allowed here.", nameof(connectionString));
                }

                break;

            case DbProviderType.Sqlite:
                // A SQLite file has no host.
                var key = builder.Keys.Cast<string>().FirstOrDefault(k => Normalize(k) is "datasource" or "filename");
                if (key is not null && SqliteFile(policy, Convert.ToString(builder[key], CultureInfo.InvariantCulture) ?? string.Empty) is { } path)
                {
                    builder[key] = path;
                    return builder.ConnectionString;
                }

                return connectionString;
        }

        if (policy.AllowedHosts.Count > 0)
        {
            var hosts = (Setting(HostKeys) ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(HostName)
                .ToList();
            var refused = hosts.Count == 0 ? "(none)" : hosts.FirstOrDefault(h => !policy.AllowedHosts.Any(p => Matches(h, p)));
            if (refused is not null)
            {
                throw new ArgumentException($"The host {refused} is not one datasources may connect to here.", nameof(connectionString));
            }
        }

        return connectionString;
    }

    // The file's absolute path, under the allowed folder; null for an in-memory database.
    private static string? SqliteFile(DapperMaticConnectionPolicy policy, string dataSource)
    {
        var inMemory = dataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase)
            || dataSource.Contains("mode=memory", StringComparison.OrdinalIgnoreCase)
            || dataSource.StartsWith("file::memory:", StringComparison.OrdinalIgnoreCase);
        if (inMemory)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(policy.SqliteDirectory))
        {
            throw new ArgumentException(
                "SQLite database files are not allowed here; set ConnectionPolicy.SqliteDirectory to allow files in one folder.",
                nameof(dataSource)
            );
        }

        var path = dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ? dataSource[5..].Split('?')[0] : dataSource;
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(policy.SqliteDirectory)) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, path));
        if (!full.StartsWith(root, StringComparison.Ordinal))
        {
            throw new ArgumentException("SQLite database files must be in the folder this application allows.", nameof(dataSource));
        }

        return full;
    }

    // tcp:host,1433 or host\instance or host:5432 -> host.
    private static string HostName(string server)
    {
        var host = server.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase) ? server[4..] : server;
        host = host.Split('\\')[0];
        return host.StartsWith('[') ? host.Split(']')[0].TrimStart('[') : host.Split(':')[0];
    }

    private static bool Matches(string host, string pattern) =>
        Regex.IsMatch(host, "^" + Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal) + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string Normalize(string key) => key.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    private static bool IsTrue(string? value) =>
        value is not null && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase) || value == "1");
}
