namespace DbLive;

/// <summary>
/// Code files failed: those of a round that deployed no file of their group, so the order of the files is not the cause, a file whose
/// error ended the deployment transaction, or a file that ended the transaction it ran in with <c>COMMIT</c> or <c>ROLLBACK</c>.
/// The inner exceptions are the <see cref="CodeDeploymentException"/> of each file, which name it.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class CodeDeploymentAggregateException : AggregateException
{
	internal CodeDeploymentAggregateException(string errorMessage, IEnumerable<Exception> innerExceptions)
		: base(errorMessage, innerExceptions)
	{
	}
}
