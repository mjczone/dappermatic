// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using Xunit.Abstractions;

namespace MJCZone.DapperMatic.AspNetCore.Tests.Services;

/// <summary>
/// Unit tests for DapperMatic service constraint operations.
/// </summary>
public partial class DapperMaticServiceTests(
    TestcontainersAssemblyFixture fixture,
    ITestOutputHelper outputHelper
) : DapperMaticServiceTestBase(fixture, outputHelper) { }
