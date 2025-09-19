// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace Akov.NetDocsProcessor.Input;

/// <summary>
/// Represents the access level for types and members that should be put into the documentation.
/// </summary>
public enum AccessLevel
{
    /// <summary>
    /// Only public members to documentation.
    /// </summary>
    Public,
    
    /// <summary>
    /// Public + protected members to documentation.
    /// </summary>
    Protected,
    
    /// <summary>
    /// Public + internal members to documentation.
    /// </summary>
    Internal,
    
    /// <summary>
    /// Public + protected + internal members to documentation. 
    /// </summary>
    ProtectedInternal,
    
    /// <summary>
    /// All members to documentation.
    /// </summary>
    Private
}