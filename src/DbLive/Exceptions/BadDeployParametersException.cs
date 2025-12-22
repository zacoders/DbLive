namespace EasyFlow.Exceptions;

[ExcludeFromCodeCoverage]
public class BadDeployParametersException : Exception
{
	public BadDeployParametersException(string message)
		: base(message)
	{
	}
}