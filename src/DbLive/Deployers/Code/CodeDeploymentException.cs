namespace DbLive;

[ExcludeFromCodeCoverage]
public sealed class CodeDeploymentException : Exception
{
	internal CodeDeploymentException(string errorMessage, Exception innerException)
		: base(errorMessage, innerException)
	{
	}
}
