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
    public void ValidateViewDefinition_ValidSelectStatement_ShouldNotThrow()
    {
        var validView = "SELECT id, name FROM users WHERE active = 1";
        
        var exception = Record.Exception(() => 
            SqlExpressionValidator.ValidateViewDefinition(validView));
        
        Assert.Null(exception);
    }

    [Fact]
    public void ValidateViewDefinition_WithSqlInjection_ShouldThrow()
    {
        var maliciousView = "SELECT 1; DROP TABLE users; --";
        
        var exception = Assert.Throws<ArgumentException>(() => 
            SqlExpressionValidator.ValidateViewDefinition(maliciousView));
        
        Assert.Contains("potentially dangerous", exception.Message);
    }

    [Fact]
    public void ValidateViewDefinition_NotStartingWithSelect_ShouldThrow()
    {
        var invalidView = "DROP TABLE users";
        
        var exception = Assert.Throws<ArgumentException>(() => 
            SqlExpressionValidator.ValidateViewDefinition(invalidView));
        
        Assert.Contains("must start with SELECT", exception.Message);
    }

    [Fact]
    public void ValidateViewDefinition_WithDangerousKeywords_ShouldThrow()
    {
        var maliciousView = "SELECT * FROM users; EXEC xp_cmdshell 'dir'";
        
        var exception = Assert.Throws<ArgumentException>(() => 
            SqlExpressionValidator.ValidateViewDefinition(maliciousView));
        
        Assert.Contains("potentially dangerous", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ValidateViewDefinition_EmptyOrNull_ShouldThrow(string? invalidView)
    {
        var exception = Assert.Throws<ArgumentException>(() => 
            SqlExpressionValidator.ValidateViewDefinition(invalidView!));
        
        Assert.Contains("cannot be null or empty", exception.Message);
    }

    #endregion

    #region Check Expression Tests

    [Fact]  
    public void ValidateCheckExpression_ValidExpression_ShouldNotThrow()
    {
        var validCheck = "status IN ('A', 'B', 'C')";
        
        var exception = Record.Exception(() => 
            SqlExpressionValidator.ValidateCheckExpression(validCheck));
        
        Assert.Null(exception);
    }

    [Fact]
    public void ValidateCheckExpression_WithSqlInjection_ShouldThrow()
    {
        var maliciousCheck = "status = 'A'; DROP TABLE users; --";
        
        var exception = Assert.Throws<ArgumentException>(() => 
            SqlExpressionValidator.ValidateCheckExpression(maliciousCheck));
        
        Assert.Contains("potentially dangerous", exception.Message);
    }

    [Fact]
    public void ValidateCheckExpression_WithDangerousKeywords_ShouldThrow()
    {
        var maliciousCheck = "status = 'A' OR (SELECT COUNT(*) FROM passwords) > 0";
        
        var exception = Assert.Throws<ArgumentException>(() => 
            SqlExpressionValidator.ValidateCheckExpression(maliciousCheck));
        
        Assert.Contains("SELECT", exception.Message);
    }

    [Fact]
    public void ValidateCheckExpression_WithComments_ShouldThrow()
    {
        var maliciousCheck = "status = 'A' /* comment */";
        
        var exception = Assert.Throws<ArgumentException>(() => 
            SqlExpressionValidator.ValidateCheckExpression(maliciousCheck));
        
        Assert.Contains("comment", exception.Message);
    }

    #endregion

    #region Default Expression Tests

    [Fact]
    public void ValidateDefaultExpression_ValidLiteral_ShouldNotThrow()
    {
        var validDefault = "'default_value'";
        
        var exception = Record.Exception(() => 
            SqlExpressionValidator.ValidateDefaultExpression(validDefault));
        
        Assert.Null(exception);
    }

    [Fact]
    public void ValidateDefaultExpression_ValidFunction_ShouldNotThrow()
    {  
        var validDefault = "GETDATE()";
        
        var exception = Record.Exception(() => 
            SqlExpressionValidator.ValidateDefaultExpression(validDefault));
        
        Assert.Null(exception);
    }

    [Fact]
    public void ValidateDefaultExpression_WithSqlInjection_ShouldThrow()
    {
        var maliciousDefault = "'test'; DROP TABLE users; --";
        
        var exception = Assert.Throws<ArgumentException>(() => 
            SqlExpressionValidator.ValidateDefaultExpression(maliciousDefault));
        
        Assert.Contains("potentially dangerous", exception.Message);
    }

    [Fact]
    public void ValidateDefaultExpression_WithDangerousKeywords_ShouldThrow()
    {
        var maliciousDefault = "(SELECT password FROM users WHERE id = 1)";
        
        var exception = Assert.Throws<ArgumentException>(() => 
            SqlExpressionValidator.ValidateDefaultExpression(maliciousDefault));
        
        Assert.Contains("SELECT", exception.Message);
    }

    #endregion

    #region Edge Cases and Security Tests

    [Fact]
    public void ValidateViewDefinition_TooLong_ShouldThrow()
    {
        var longView = "SELECT " + new string('*', 2500);
        
        var exception = Assert.Throws<ArgumentException>(() => 
            SqlExpressionValidator.ValidateViewDefinition(longView));
        
        Assert.Contains("too long", exception.Message);
    }

    [Fact]
    public void ValidateCheckExpression_WithNullBytes_ShouldThrow()
    {
        var maliciousCheck = "status = 'A'\0";
        
        var exception = Assert.Throws<ArgumentException>(() => 
            SqlExpressionValidator.ValidateCheckExpression(maliciousCheck));
        
        Assert.Contains("invalid control characters", exception.Message);
    }

    [Fact]
    public void ValidateDefaultExpression_CommentInjection_ShouldThrow()
    {
        var maliciousDefault = "'test' --";
        
        var exception = Assert.Throws<ArgumentException>(() => 
            SqlExpressionValidator.ValidateDefaultExpression(maliciousDefault));
        
        Assert.Contains("comment", exception.Message);
    }

    [Theory]
    [InlineData("'; DROP TABLE users; --")]
    [InlineData("1=1; EXEC xp_cmdshell 'format c:'")]
    [InlineData("1 OR (SELECT COUNT(*) FROM sys.tables) > 0")]
    [InlineData("1 UNION SELECT password FROM users")]
    public void ValidateCheckExpression_VariousSqlInjectionAttempts_ShouldThrow(string maliciousExpression)
    {
        var exception = Assert.Throws<ArgumentException>(() => 
            SqlExpressionValidator.ValidateCheckExpression(maliciousExpression));
        
        Assert.NotNull(exception);
    }

    [Theory]
    [InlineData("WAITFOR DELAY '00:00:05'")]
    [InlineData("SHUTDOWN")]
    [InlineData("BACKUP DATABASE test TO DISK = 'C:\\test.bak'")]
    [InlineData("RESTORE DATABASE test FROM DISK = 'C:\\test.bak'")]
    public void ValidateViewDefinition_SystemCommands_ShouldThrow(string maliciousCommand)
    {
        var maliciousView = $"SELECT 1; {maliciousCommand}; --";
        
        var exception = Assert.Throws<ArgumentException>(() => 
            SqlExpressionValidator.ValidateViewDefinition(maliciousView));
        
        Assert.NotNull(exception);
    }

    #endregion
}