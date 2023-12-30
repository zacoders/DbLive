using EasyFlow.Adapter.MSSQL;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace TestProject1;

public class EasyFlowTestingMSSQL : EasyFlowTesting
{
	public EasyFlowTestingMSSQL(string projectPath, string dbConnectionString, ITestOutputHelper output)
		: base(CreateContainer(), projectPath, dbConnectionString, output)
	{
	}
	private static ServiceCollection CreateContainer()
	{
		// TODO: I think we can create factory or class what will create and initialize contaner for specifig sql engine.
		//		 It can be used everywhere where a new container needed.
		var container = new ServiceCollection();
		container.InitializeMSSQL();
		return container;
	}
}
