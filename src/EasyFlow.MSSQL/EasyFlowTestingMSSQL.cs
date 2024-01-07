using EasyFlow.Common;
using EasyFlow.Testing;

namespace EasyFlow.MSSQL;

public class EasyFlowTestingMSSQL(string projectPath, string dbConnectionString)
	: EasyFlowTesting(CreateContainer(), projectPath, dbConnectionString)
{
	private static EasyFlowBuilder CreateContainer()
	{
		var builder = new EasyFlowBuilder();

		builder.SqlServer();

		return builder;
	}
}
