// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.DataAnnotations;

/// <summary>
/// Marks the property the DML methods set to the time a row is inserted or updated, from <c>DmDefaults.Clock</c>,
/// in UTC: a <c>DateTime</c> or a <c>DateTimeOffset</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class DmUpdatedAtAttribute : Attribute { }
