namespace DbLive.Deployers.Migrations;

[ExcludeFromCodeCoverage]
public class MigrationDeploymentException: Exception
{
	public MigrationDeploymentException(string errorMessage, Exception innerException)
		: base(errorMessage, innerException)
	{
	}

	public MigrationDeploymentException(string message) : base(message)
	{
	}
}
