namespace DbLive.Deployers.Migrations;

[ExcludeFromCodeCoverage]
public class MigrationDeploymentException(string errorMessage, Exception innerException)
	: Exception(errorMessage, innerException)
{
}
