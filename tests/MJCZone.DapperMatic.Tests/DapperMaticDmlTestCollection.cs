// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// Collection definition for DapperMatic DML type mapping tests.
/// Disables parallelization due to global Dapper type mapping state (SqlMapper.TypeMapProvider).
/// </summary>
[CollectionDefinition("DapperMaticDmlTests", DisableParallelization = true)]
public class DapperMaticDmlTestCollection { }
