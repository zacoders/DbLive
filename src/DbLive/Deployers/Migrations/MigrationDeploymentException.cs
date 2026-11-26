namespace DbLive;

/// <summary>
/// A migration or breaking script of a migration failed: its SQL error, its timeout, the timeout of its transaction, or a script that
/// ended its transaction. The message names the file; the inner exception is the error, usually a <see cref="DbLiveSqlException"/>.
/// The script is not marked applied: in a transaction it was rolled back, without one it may be partly applied and runs again
/// from the start.
/// </summary>
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
