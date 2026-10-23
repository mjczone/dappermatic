// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

#pragma warning disable SA1402 // A run of several schemas and each schema's outcome belong together.

namespace MJCZone.DapperMatic.Migrations;

/// <summary>
/// What <see cref="DmMigrator.MigrateSchemasAsync"/> did, schema by schema.
/// </summary>
public sealed class DmSchemaMigrationResult
{
    /// <summary>Gets each schema's outcome: the shared schema's first, then the others in the order listed.</summary>
    public required IReadOnlyList<DmSchemaMigrationOutcome> Schemas { get; init; }

    /// <summary>Gets a value indicating whether every schema was migrated.</summary>
    public bool Succeeded => Schemas.All(s => s.Exception is null);

    /// <summary>Gets the schemas that failed, which a later run migrates again: nothing of theirs was recorded.</summary>
    public IEnumerable<DmSchemaMigrationOutcome> Failed => Schemas.Where(s => s.Exception is not null);
}

/// <summary>
/// One schema's part of a <see cref="DmMigrator.MigrateSchemasAsync"/> run.
/// </summary>
public sealed class DmSchemaMigrationOutcome
{
    /// <summary>Gets the schema; null for the provider's default, where the shared models are when no shared schema is set.</summary>
    public required string? SchemaName { get; init; }

    /// <summary>Gets a value indicating whether the schema holds the shared models.</summary>
    public bool IsShared { get; init; }

    /// <summary>Gets what the schema's run did, or null if it failed.</summary>
    public DmMigrationResult? Result { get; init; }

    /// <summary>Gets why the schema's run failed, or null if it did not.</summary>
    public Exception? Exception { get; init; }
}
