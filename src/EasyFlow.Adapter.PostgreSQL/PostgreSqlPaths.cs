namespace EasyFlow.Adapter.MSSQL;

internal class PostgreSqlPaths : IEasyFlowPaths
{
    public string GetPathToEasyFlowSelfProject() =>
        Path.Combine(AppContext.BaseDirectory, "EasySqlFlow_PostgreSQL");
}