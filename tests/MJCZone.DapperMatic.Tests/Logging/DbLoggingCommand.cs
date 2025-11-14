using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.Logging;

namespace MJCZone.DapperMatic.Tests.Logging;

public class DbLoggingCommand : DbCommand
{
    public DbCommand Inner { get; }
    private readonly ILogger _logger;

    public DbLoggingCommand(DbCommand inner, ILogger logger)
    {
        Inner = inner;
        _logger = logger;
    }

    public override void Cancel()
    {
        Inner.Cancel();
    }

    public override int ExecuteNonQuery()
    {
        return Execute(() => Inner.ExecuteNonQuery());
    }

    public override object? ExecuteScalar()
    {
        return Execute(Inner.ExecuteScalar);
    }

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        return Execute(() => Inner.ExecuteReader(behavior));
    }

    private string BuildSqlStatement()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"- CommandType {CommandType}");
        sb.AppendLine($"- CommandText {CommandText}");

        if (Parameters.Count > 0)
            sb.AppendLine("- Parameters");

        foreach (IDataParameter parameter in Parameters)
        {
            sb.AppendLine($"\t{parameter.ParameterName} = {parameter.Value}");
        }
        return sb.ToString();
    }

    private T Execute<T>(Func<T> func)
    {
        var sqlStatement = BuildSqlStatement();
        _logger.LogDebug($"Executing\n{sqlStatement}");

        try
        {
            return func();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error on executing\n{sqlStatement}");
            throw;
        }
    }

    #region Passthrough

    public override void Prepare()
    {
        Inner.Prepare();
    }

    [AllowNull]
    public override string CommandText
    {
        get => Inner.CommandText;
        set => Inner.CommandText = value;
    }

    public override int CommandTimeout
    {
        get => Inner.CommandTimeout;
        set => Inner.CommandTimeout = value;
    }

    public override CommandType CommandType
    {
        get => Inner.CommandType;
        set => Inner.CommandType = value;
    }

    public override UpdateRowSource UpdatedRowSource
    {
        get => Inner.UpdatedRowSource;
        set => Inner.UpdatedRowSource = value;
    }

    protected override DbConnection? DbConnection
    {
        get => Inner.Connection;
        set => Inner.Connection = value;
    }

    protected override DbParameterCollection DbParameterCollection => Inner.Parameters;

    protected override DbTransaction? DbTransaction
    {
        get => Inner.Transaction;
        set => Inner.Transaction = value;
    }

    public override bool DesignTimeVisible
    {
        get => Inner.DesignTimeVisible;
        set => Inner.DesignTimeVisible = value;
    }

    protected override DbParameter CreateDbParameter()
    {
        return Inner.CreateParameter();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Inner?.Dispose();
        }
        base.Dispose(disposing);
    }

    #endregion
}
