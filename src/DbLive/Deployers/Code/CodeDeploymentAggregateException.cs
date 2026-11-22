namespace DbLive;

/// <summary>
/// Code files failed: those of a round that deployed no file of their group, so the order of the files is not the cause, or a file whose
/// error ended the deployment transaction. The inner exceptions are the <see cref="CodeDeploymentException"/> of each file, which name it.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class CodeDeploymentAggregateException : AggregateException
{
	internal CodeDeploymentAggregateException(string errorMessage, IEnumerable<Exception> innerExceptions)
		: base(errorMessage, innerExceptions)
	{
	}
}
