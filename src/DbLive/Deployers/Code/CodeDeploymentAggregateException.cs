namespace DbLive.Deployers.Code;

[ExcludeFromCodeCoverage]
public class CodeDeploymentAggregateException(string errorMessage, IEnumerable<Exception> innerExceptions) 
	: AggregateException(errorMessage, innerExceptions)
{
}
