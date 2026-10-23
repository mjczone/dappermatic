// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Dapper;
using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.Tests;

public abstract partial class DatabaseMethodsTests
{
    /// <summary>
    /// Values set on a connection's session, as the database's own row-level security reads them.
    /// </summary>
    [Fact]
    protected virtual async Task Session_values_are_read_by_the_database_Async()
    {
        using var db = await OpenConnectionAsync();
        var provider = db.GetDbProviderType();
        if (provider == DbProviderType.Sqlite)
        {
            await Assert.ThrowsAsync<NotSupportedException>(() => DmSession.SetAsync(db, [new("app.tenant_id", "t1")]));
            return;
        }

        if (provider == DbProviderType.MySql)
        {
            // The test connections do not allow user variables: the refusal says what to add.
            var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => DmSession.SetAsync(db, [new("dm_tenant", "t1")]));
            Assert.Contains("AllowUserVariables=True", refused.Message);
            return;
        }

        var (name, read) = provider switch
        {
            DbProviderType.PostgreSql => ("app.dm_tenant", "SELECT current_setting('app.dm_tenant', true)"),
            DbProviderType.SqlServer => ("dm_tenant", "SELECT CAST(SESSION_CONTEXT(N'dm_tenant') AS nvarchar(100))"),
            _ => throw new NotSupportedException(),
        };

        await DmSession.SetAsync(db, [new(name, new[] { 2, 3, 5 })]);
        Assert.Equal("2,3,5", await db.ExecuteScalarAsync<string>(read));

        if (provider == DbProviderType.PostgreSql)
        {
            await Assert.ThrowsAsync<ArgumentException>(() => DmSession.SetAsync(db, [new("tenant", "x")]));
        }
    }
}
