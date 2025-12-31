namespace DbLive.Deployers.Code;

[ExcludeFromCodeCoverage]
public class MigrationDeploymentException(string errorMessage, Exception innerException)
	: Exception(errorMessage, innerException)
{
}
