// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Providers.MySql;

namespace MJCZone.DapperMatic.Tests;

/// <summary>
/// MySQL's MODIFY COLUMN replaces the whole column definition, so altering a column means restating the
/// definition from SHOW CREATE TABLE with only the type and nullability changed. Anything else that moves
/// is lost data or lost behaviour.
/// </summary>
public class MySqlColumnDefinitionTests
{
    [Theory]
    // Character set, collation, default and comment are kept in place.
    [InlineData(
        "varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL DEFAULT 'abc' COMMENT 'the title'",
        "varchar(200)",
        true,
        "varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NULL DEFAULT 'abc' COMMENT 'the title'"
    )]
    // DEFAULT NULL is a default, not nullability.
    [InlineData("varchar(50) DEFAULT NULL", "varchar(200)", true, "varchar(200) DEFAULT NULL")]
    // ...but it is invalid on a NOT NULL column, so it goes.
    [InlineData("varchar(50) DEFAULT NULL", "varchar(200)", false, "varchar(200) NOT NULL")]
    // Numeric modifiers belong to the type and are replaced with it.
    [InlineData(
        "decimal(10,2) unsigned NOT NULL DEFAULT '0.00'",
        "decimal(12,2) unsigned",
        false,
        "decimal(12,2) unsigned NOT NULL DEFAULT '0.00'"
    )]
    // NULL inside a quoted string is text, not a clause.
    [InlineData("int NOT NULL COMMENT 'may be NULL later'", "int", true, "int NULL COMMENT 'may be NULL later'")]
    [InlineData("varchar(20) NOT NULL DEFAULT 'it''s NULL'", "varchar(40)", true, "varchar(40) NULL DEFAULT 'it''s NULL'")]
    [InlineData(
        @"varchar(20) NOT NULL DEFAULT 'a\' NOT NULL'",
        "varchar(40)",
        true,
        @"varchar(40) NULL DEFAULT 'a\' NOT NULL'"
    )]
    // A generated column keeps its expression, and nullability goes after the storage keyword.
    [InlineData(
        "varchar(50) GENERATED ALWAYS AS (upper(`label`)) VIRTUAL",
        "varchar(100)",
        true,
        "varchar(100) GENERATED ALWAYS AS (upper(`label`)) VIRTUAL"
    )]
    [InlineData(
        "varchar(50) GENERATED ALWAYS AS (upper(`label`)) STORED NOT NULL",
        "varchar(100)",
        true,
        "varchar(100) GENERATED ALWAYS AS (upper(`label`)) STORED NULL"
    )]
    // ON UPDATE and AUTO_INCREMENT survive.
    [InlineData(
        "timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP",
        "timestamp",
        true,
        "timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP"
    )]
    [InlineData("int NOT NULL AUTO_INCREMENT", "bigint", false, "bigint NOT NULL AUTO_INCREMENT")]
    // A type whose arguments contain quotes and spaces is replaced whole.
    [InlineData("enum('a','b c') NOT NULL", "enum('a','b c','d')", false, "enum('a','b c','d') NOT NULL")]
    public void Restating_a_column_changes_only_its_type_and_nullability(
        string definition,
        string sqlType,
        bool isNullable,
        string expected
    )
    {
        Assert.Equal(expected, MySqlMethods.RestateColumnDefinition(definition, sqlType, isNullable));
    }
}
