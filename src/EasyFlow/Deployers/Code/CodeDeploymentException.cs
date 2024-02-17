namespace EasyFlow.Adapter;

[ExcludeFromCodeCoverage]
public class CodeDeploymentException : Exception
{
	public CodeDeploymentException(string errorMessage)
		: base(errorMessage)
	{
	}
}
