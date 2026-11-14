namespace DbLive;

[ExcludeFromCodeCoverage]
public sealed class MigrationDeploymentException : Exception
{
	internal MigrationDeploymentException(string errorMessage, Exception innerException)
		: base(errorMessage, innerException)
	{
	}

	internal MigrationDeploymentException(string message) : base(message)
	{
	}
}
