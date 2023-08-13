namespace EasyFlow.Adapter.PostgreSQL;

public class PostgreSqlDA : IEasyFlowDA
{
	public bool EasyFlowInstalled(string cnnString)
	{
		throw new NotImplementedException();
	}

	public IReadOnlyCollection<MigrationDto> GetMigrations(string domain, string cnnString)
	{
		throw new NotImplementedException();
	}
}
