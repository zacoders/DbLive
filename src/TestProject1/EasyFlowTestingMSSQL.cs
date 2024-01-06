using EasyFlow.Common;
using EasyFlow.MSSQL;

namespace TestProject1;

public abstract class EasyFlowTestingMSSQL(string projectPath, string dbConnectionString)
	: EasyFlowTesting(CreateContainer(), projectPath, dbConnectionString)
{
	private static EasyFlowBuilder CreateContainer()
	{
		var builder = new EasyFlowBuilder();

		builder.SqlServer();

		return builder;
	}
}
