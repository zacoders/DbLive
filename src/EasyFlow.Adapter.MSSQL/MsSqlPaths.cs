namespace EasyFlow.Adapter.MSSQL;

internal class MsSqlPaths : IEasyFlowPaths
{
    public string GetPathToEasyFlowSelfProject() =>
        Path.Combine(AppContext.BaseDirectory, "EasySqlFlow_MSSQL");
}