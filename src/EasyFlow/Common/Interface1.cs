namespace EasyFlow.Common;

public interface IEasyFlowDbConnection
{
	string ConnectionString { get; }
}

public class EasyFlowDbConnection(string connectionString) : IEasyFlowDbConnection
{
	public string ConnectionString { get; } = connectionString;
}


public interface IEasyFlowProjectPath
{
	string ProjectPath { get; }
}

public class EasyFlowProjectPath(string projectPath) : IEasyFlowProjectPath
{
	public string ProjectPath { get; } = projectPath;
}