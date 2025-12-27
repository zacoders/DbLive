namespace DbLive.Deployers.Code;

[ExcludeFromCodeCoverage]
public class CodeDeploymentException(string errorMessage, Exception innerException)
	: Exception(errorMessage, innerException)
{
}
