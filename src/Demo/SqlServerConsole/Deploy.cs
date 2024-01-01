using EasyFlow;
using EasyFlow.Adapter.MSSQL;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace SqlServerConsole;

public class Deploy
{
	public static IEasyFlow DeployToSqlServer()
	{
		var container = new ServiceCollection();
		container.InitializeEasyFlow();
		container.InitializeMSSQL();

		var logger = new LoggerConfiguration()
			.WriteTo.Console()
			.CreateLogger();

		container.AddSingleton<ILogger>(logger);


		var serviceProvider = container.BuildServiceProvider();

		var sqlDeploy = serviceProvider.GetService<IEasyFlow>()!;

		return sqlDeploy;
	}
}