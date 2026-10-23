// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.DataAnnotations;

/// <summary>
/// Marks the property that says which tenant a row belongs to. Every DML method then reads and writes only the
/// scope's tenant's rows (<c>ForTenant(id)</c>, or <c>DmDefaults.TenantId</c>), and sets it on inserted rows; with no
/// tenant, the methods refuse the model, unless the scope says <c>AllTenants()</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class DmTenantKeyAttribute : Attribute { }
