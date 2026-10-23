// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.DataAnnotations;

/// <summary>
/// Marks the property the DML methods set to the user who inserts a row, from the scope's <c>WithUser</c> or
/// <c>DmDefaults.User</c>, converted to the property's type. Updates never change it, and it is left as it is when
/// there is no user.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class DmCreatedByAttribute : Attribute { }
