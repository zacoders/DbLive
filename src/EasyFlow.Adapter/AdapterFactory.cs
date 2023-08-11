using EasyFlow.Adapter.MSSQL;
using EasyFlow.Adapter.PostgreSQL;
using Microsoft.Extensions.DependencyInjection;

namespace EasyFlow.Adapter;

internal class AdapterFactory : IAdapterFactory
{
	private readonly IServiceProvider _serviceProvider;

	public AdapterFactory(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider;
	}

	public IEasyFlowDeployer GetDeployer(DBEngine dbEngine)
	{
		IEasyFlowDeployer? deployer = dbEngine switch
		{
			DBEngine.MSSQL => _serviceProvider.GetService<MsSqlDeployer>(),
			DBEngine.PostgreSql => _serviceProvider.GetService<PostgreSqlDeployer>(),
			_ => throw new NotImplementedException($"Unsupported DBEngine '{dbEngine}' provided.")
		};

		return deployer ?? throw new DeployerCannotBeResolved(dbEngine);
	}

	public IEasyFlowDA GetDataAccess(DBEngine dbEngine)
	{
		IEasyFlowDA? deployer = dbEngine switch
		{
			DBEngine.MSSQL => _serviceProvider.GetService<MsSqlDA>(),
			DBEngine.PostgreSql => _serviceProvider.GetService<PostgreSqlDA>(),
			_ => throw new NotImplementedException($"Unsupported DBEngine '{dbEngine}' provided.")
		};

		return deployer ?? throw new DataAccessCannotBeResolved(dbEngine);
	}
}
