namespace DbLive;

[ExcludeFromCodeCoverage]
public sealed class CodeDeploymentAggregateException : AggregateException
{
	internal CodeDeploymentAggregateException(string errorMessage, IEnumerable<Exception> innerExceptions)
		: base(errorMessage, innerExceptions)
	{
	}
}
