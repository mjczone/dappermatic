// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Security;

namespace MJCZone.DapperMatic.AspNetCore.Security;

/// <summary>
/// Authorizes operations by the policies: nothing is allowed unless a policy grants it. Reading and writing rows is
/// decided table by table, with each table's columns, as the rows are read or written; this lets those operations
/// through to that.
/// </summary>
public class DefaultDapperMaticPermissions : IDapperMaticPermissions
{
    private readonly DapperMaticPolicySet _policies;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultDapperMaticPermissions"/> class.
    /// </summary>
    /// <param name="policies">The policies in force.</param>
    public DefaultDapperMaticPermissions(DapperMaticPolicySet policies)
    {
        ArgumentNullException.ThrowIfNull(policies);
        _policies = policies;
    }

    /// <inheritdoc />
    public virtual Task<bool> IsAuthorizedAsync(DapperMaticOperation context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var action = DapperMaticPolicySet.ActionOf(context.Operation);
        if (action is DmAction.Read or DmAction.Create or DmAction.Update or DmAction.Delete)
        {
            return Task.FromResult(true);
        }

        var access = (context.Access ?? new DmAccessContext(context.User)).ForDatasource(context.DatasourceId);
        return Task.FromResult(_policies.Evaluate(access, DapperMaticPolicySet.ResourceOf(context), action).Allowed);
    }
}
