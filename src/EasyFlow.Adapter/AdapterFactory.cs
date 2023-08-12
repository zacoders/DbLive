namespace EasyFlow.Adapter;

internal class AdapterFactory : IAdapterFactory
{
	private readonly IServiceProvider _serviceProvider;

	public AdapterFactory(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider;
	}

	public IEasyFlowSqlConnection GetDeployer(DBEngine dbEngine, string connectionString)
	{
		IEasyFlowSqlConnection deployer = dbEngine switch
		{
			DBEngine.MSSQL => MsSqlDeployer.OpenConnection(connectionString),
			DBEngine.PostgreSql => PostgreSqlDeployer.OpenConnection(connectionString),
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
