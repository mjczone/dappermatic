// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.DataAnnotations;

/// <summary>
/// Marks the property that changes on every write, so that a row written by key is written only if no one else has
/// written it since it was read: an <c>int</c> or <c>long</c> (incremented), a <c>Guid</c> (new on each write), or a
/// <c>byte[]</c> (new random bytes, or on SQL Server a <c>rowversion</c> column the database writes). A row that
/// changed throws <c>DmConcurrencyException</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class DmConcurrencyTokenAttribute : Attribute { }
