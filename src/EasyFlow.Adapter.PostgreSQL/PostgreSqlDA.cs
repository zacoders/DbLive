namespace EasyFlow.Adapter.PostgreSQL;

public class PostgreSqlDA : IEasyFlowDA
{
	public bool EasyFlowInstalled(string cnnString)
	{
		throw new NotImplementedException();
	}

	public int GetEasyFlowVersion(string cnnString)
	{
		throw new NotImplementedException();
	}

	public IReadOnlyCollection<MigrationDto> GetMigrations(string cnnString)
	{
		throw new NotImplementedException();
	}

	public void MigrationCompleted(string cnnString, int migrationVerion, string migrationName, DateTime migrationStartedUtc, DateTime migrationCompletedUtc)
	{
		throw new NotImplementedException();
	}

	public void SetEasyFlowVersion(string cnnString, int version, DateTime migrationCompletedUtc)
	{
		throw new NotImplementedException();
	}
}
