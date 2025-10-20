// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace Akov.NetDocsProcessor.Input;

/// <summary>
/// The set of settings for the documentation output.
/// </summary>
public class GenerationSettings
{
    /// <summary>
    /// Defines which types and members will be removed from the output.
    /// The default value is AccessLevel.Public.
    /// </summary>
    public AccessLevel AccessLevel { get; set; } = AccessLevel.Public;
}
