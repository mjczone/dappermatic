// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Performance",
    "CA1848:Use the LoggerMessage delegates",
    Justification = "Intentional",
    Scope = "namespaceanddescendants",
    Target = "~N:MJCZone.DapperMatic.AspNetCore"
)]
[assembly: SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Justification = "Intentional",
    Scope = "namespaceanddescendants",
    Target = "~N:MJCZone.DapperMatic.AspNetCore"
)]
