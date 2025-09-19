// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

namespace MJCZone.DapperMatic
{
    /// <summary>
    /// Specifies the type affinity for a database column.
    /// </summary>
    public enum TypeAffinity
    {
        /// <summary>
        /// Represents an integer type.
        /// </summary>
        Integer,

        /// <summary>
        /// Represents a real number type.
        /// </summary>
        Real,

        /// <summary>
        /// Represents a boolean type.
        /// </summary>
        Boolean,

        /// <summary>
        /// Represents a DateTime type.
        /// </summary>
        DateTime,

        /// <summary>
        /// Represents a text type.
        /// </summary>
        Text,

        /// <summary>
        /// Represents a binary type.
        /// </summary>
        Binary,

        /// <summary>
        /// Represents a geometry type.
        /// </summary>
        Geometry,

        /// <summary>
        /// Represents a range type.
        /// </summary>
        RangeType,

        /// <summary>
        /// Represents other types not covered by the above.
        /// </summary>
        Other,
    }
}
