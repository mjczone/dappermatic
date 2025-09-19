// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using System.Data;
using Xunit.Abstractions;

namespace MJCZone.DapperMatic.Tests;

public abstract class TestBase : IDisposable
{
    protected readonly ITestOutputHelper Output;

    protected TestBase(ITestOutputHelper output)
    {
        Output = output;
    }

    protected async Task InitFreshSchemaAsync(IDbConnection db, string? schemaName)
    {
        if (db.SupportsSchemas())
        {
            foreach (var view in await db.GetViewsAsync(schemaName))
            {
                try
                {
                    await db.DropViewIfExistsAsync(schemaName, view.ViewName);
                }
                catch { }
            }
            foreach (var table in await db.GetTablesAsync(schemaName))
            {
                await db.DropTableIfExistsAsync(schemaName, table.TableName);
            }
            // await db.DropSchemaIfExistsAsync(schemaName);
        }
        if (!string.IsNullOrEmpty(schemaName))
        {
            await db.CreateSchemaIfNotExistsAsync(schemaName);
        }
    }

    public virtual void Dispose()
    {
        // nothing to dispose
    }

    protected void Log(string message)
    {
        Output.WriteLine(message);
    }

    public virtual bool IgnoreSqlType(string sqlType) => false;
}
