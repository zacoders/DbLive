namespace EasyFlow.Adapter.PostgreSQL;

public class PostgreSqlDA : IEasyFlowDA
{
	public IReadOnlyCollection<MigrationDto> GetMigrations(string cnnString)
	{
		throw new NotImplementedException();
	}
}
