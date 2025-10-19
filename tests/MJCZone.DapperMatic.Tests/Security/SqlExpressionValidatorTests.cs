// Copyright 2025 MJCZone Inc.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Licensed under the GNU Lesser General Public License v3.0 or later.
// See LICENSE in the project root for license information.

using MJCZone.DapperMatic.Security;
using Xunit;

namespace MJCZone.DapperMatic.Tests.Security;

/// <summary>
/// Tests for SQL injection prevention in expression validation.
/// </summary>
public class SqlExpressionValidatorTests
{
    #region View Definition Tests

    [Fact]
    public void Should_validate_view_definition_valid_select_statement_does_not_throw()
    {
        var validView = "SELECT id, name FROM users WHERE active = 1";

        var exception = Record.Exception(() =>
            SqlExpressionValidator.ValidateViewDefinition(validView)
        );

        Assert.Null(exception);
    }

    [Fact]
    public void Should_validate_view_definition_with_sql_injection_does_throw()
    {
        var maliciousView = "SELECT 1; DROP TABLE users; --";

        var exception = Assert.Throws<ArgumentException>(() =>
            SqlExpressionValidator.ValidateViewDefinition(maliciousView)
        );

        Assert.Contains("potentially dangerous", exception.Message);
    }

    [Fact]
    public void Should_validate_view_definition_not_starting_with_select_does_throw()
    {
        var invalidView = "DROP TABLE users";

        var exception = Assert.Throws<ArgumentException>(() =>
            SqlExpressionValidator.ValidateViewDefinition(invalidView)
        );

        Assert.Contains("must start with SELECT", exception.Message);
    }

    [Fact]
    public void Should_validate_view_definition_with_dangerous_keywords_does_throw()
    {
        var maliciousView = "SELECT * FROM users; EXEC xp_cmdshell 'dir'";

        var exception = Assert.Throws<ArgumentException>(() =>
            SqlExpressionValidator.ValidateViewDefinition(maliciousView)
        );

        Assert.Contains("potentially dangerous", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Should_validate_view_definition_empty_or_null_does_throw(string? invalidView)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            SqlExpressionValidator.ValidateViewDefinition(invalidView!)
        );

        Assert.Contains("cannot be null or empty", exception.Message);
    }

    #endregion

    #region Check Expression Tests

    [Fact]
    public void Should_validate_check_expression_valid_expression_does_not_throw()
    {
        var validCheck = "status IN ('A', 'B', 'C')";

        var exception = Record.Exception(() =>
            SqlExpressionValidator.ValidateCheckExpression(validCheck)
        );

        Assert.Null(exception);
    }

    [Fact]
    public void Should_validate_check_expression_with_sql_injection_does_throw()
    {
        var maliciousCheck = "status = 'A'; DROP TABLE users; --";

        var exception = Assert.Throws<ArgumentException>(() =>
            SqlExpressionValidator.ValidateCheckExpression(maliciousCheck)
        );

        Assert.Contains("potentially dangerous", exception.Message);
    }

    [Fact]
    public void Should_validate_check_expression_with_dangerous_keywords_does_throw()
    {
        var maliciousCheck = "status = 'A' OR (SELECT COUNT(*) FROM passwords) > 0";

        var exception = Assert.Throws<ArgumentException>(() =>
            SqlExpressionValidator.ValidateCheckExpression(maliciousCheck)
        );

        Assert.Contains("SELECT", exception.Message);
    }

    [Fact]
    public void Should_validate_check_expression_with_comments_does_throw()
    {
        var maliciousCheck = "status = 'A' /* comment */";

        var exception = Assert.Throws<ArgumentException>(() =>
            SqlExpressionValidator.ValidateCheckExpression(maliciousCheck)
        );

        Assert.Contains("comment", exception.Message);
    }

    #endregion

    #region Default Expression Tests

    [Fact]
    public void Should_validate_default_expression_valid_literal_does_not_throw()
    {
        var validDefault = "'default_value'";

        var exception = Record.Exception(() =>
            SqlExpressionValidator.ValidateDefaultExpression(validDefault)
        );

        Assert.Null(exception);
    }

    [Fact]
    public void Should_validate_default_expression_valid_function_does_not_throw()
    {
        var validDefault = "GETDATE()";

        var exception = Record.Exception(() =>
            SqlExpressionValidator.ValidateDefaultExpression(validDefault)
        );

        Assert.Null(exception);
    }

    [Fact]
    public void Should_validate_default_expression_with_sql_injection_does_throw()
    {
        var maliciousDefault = "'test'; DROP TABLE users; --";

        var exception = Assert.Throws<ArgumentException>(() =>
            SqlExpressionValidator.ValidateDefaultExpression(maliciousDefault)
        );

        Assert.Contains("potentially dangerous", exception.Message);
    }

    [Fact]
    public void Should_validate_default_expression_with_dangerous_keywords_does_throw()
    {
        var maliciousDefault = "(SELECT password FROM users WHERE id = 1)";

        var exception = Assert.Throws<ArgumentException>(() =>
            SqlExpressionValidator.ValidateDefaultExpression(maliciousDefault)
        );

        Assert.Contains("SELECT", exception.Message);
    }

    #endregion

    #region Edge Cases and Security Tests

    [Fact]
    public void Should_validate_view_definition_too_long_does_throw()
    {
        var longView = "SELECT " + new string('*', 2500);

        var exception = Assert.Throws<ArgumentException>(() =>
            SqlExpressionValidator.ValidateViewDefinition(longView)
        );

        Assert.Contains("too long", exception.Message);
    }

    [Fact]
    public void Should_validate_check_expression_with_null_bytes_does_throw()
    {
        var maliciousCheck = "status = 'A'\0";

        var exception = Assert.Throws<ArgumentException>(() =>
            SqlExpressionValidator.ValidateCheckExpression(maliciousCheck)
        );

        Assert.Contains("invalid control characters", exception.Message);
    }

    [Fact]
    public void Should_validate_default_expression_comment_injection_does_throw()
    {
        var maliciousDefault = "'test' --";

        var exception = Assert.Throws<ArgumentException>(() =>
            SqlExpressionValidator.ValidateDefaultExpression(maliciousDefault)
        );

        Assert.Contains("comment", exception.Message);
    }

    [Theory]
    [InlineData("'; DROP TABLE users; --")]
    [InlineData("1=1; EXEC xp_cmdshell 'format c:'")]
    [InlineData("1 OR (SELECT COUNT(*) FROM sys.tables) > 0")]
    [InlineData("1 UNION SELECT password FROM users")]
    public void Should_validate_check_expression_various_sql_injection_attempts_does_throw(
        string maliciousExpression
    )
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            SqlExpressionValidator.ValidateCheckExpression(maliciousExpression)
        );

        Assert.NotNull(exception);
    }

    [Theory]
    [InlineData("WAITFOR DELAY '00:00:05'")]
    [InlineData("SHUTDOWN")]
    [InlineData("BACKUP DATABASE test TO DISK = 'C:\\test.bak'")]
    [InlineData("RESTORE DATABASE test FROM DISK = 'C:\\test.bak'")]
    public void Should_validate_view_definition_system_commands_does_throw(string maliciousCommand)
    {
        var maliciousView = $"SELECT 1; {maliciousCommand}; --";

        var exception = Assert.Throws<ArgumentException>(() =>
            SqlExpressionValidator.ValidateViewDefinition(maliciousView)
        );

        Assert.NotNull(exception);
    }

    #endregion
}
