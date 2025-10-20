// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.AspNetCore.Models.Dtos;

namespace MJCZone.DapperMatic.AspNetCore.Repositories;

/// <summary>
/// Repository for managing DapperMatic database datasource registrations.
/// Provnamees secure storage where connection strings can be added/updated but never retrieved after insertion.
/// </summary>
public interface IDapperMaticDatasourceRepository
{
    /// <summary>
    /// Initializes the repository, creating necessary storage structures if they don't exist.
    /// This method should be called once during application startup.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Adds a new datasource registration.
    /// </summary>
    /// <param name="datasource">The datasource to add.</param>
    /// <returns>True if added successfully, false if a datasource with the same ID already exists.</returns>
    Task<bool> AddDatasourceAsync(DatasourceDto datasource);

    /// <summary>
    /// Updates an existing datasource registration. Behaves like a patch. All NULL properties are ignored.
    /// </summary>
    /// <param name="datasource">The updated datasource information.</param>
    /// <returns>True if updated successfully, false if the datasource doesn't exist.</returns>
    Task<bool> UpdateDatasourceAsync(DatasourceDto datasource);

    /// <summary>
    /// Removes a datasource registration by ID.
    /// </summary>
    /// <param name="id">The ID of the datasource to remove.</param>
    /// <returns>True if removed successfully, false if the datasource doesn't exist.</returns>
    Task<bool> RemoveDatasourceAsync(string id);

    /// <summary>
    /// Gets a list of all registered datasources and their metadata (excluding connection strings).
    /// </summary>
    /// <param name="tag">Optional tag to filter datasources by. If null, returns all datasources.</param>
    /// <returns>A collection of datasource information without connection strings.</returns>
    Task<List<DatasourceDto>> GetDatasourcesAsync(string? tag = null);

    /// <summary>
    /// Gets datasource information by ID (excluding connection string).
    /// </summary>
    /// <param name="id">The ID of the datasource.</param>
    /// <returns>The datasource information without connection string, or null if not found.</returns>
    Task<DatasourceDto?> GetDatasourceAsync(string id);

    /// <summary>
    /// Checks if a datasource with the specified ID exists.
    /// </summary>
    /// <param name="id">The ID of the datasource to check.</param>
    /// <returns>True if the datasource exists, false otherwise.</returns>
    Task<bool> DatasourceExistsAsync(string id);

    /// <summary>
    /// Gets a connection string for internal use by DapperMatic services.
    /// This method is for internal use only and should not be exposed through APIs.
    /// </summary>
    /// <param name="id">The ID of the datasource.</param>
    /// <returns>The connection string if found, null otherwise.</returns>
    Task<string?> GetConnectionStringAsync(string id);
}
