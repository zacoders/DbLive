namespace EasyFlow;

public class BadDeployParametersException : Exception
{
	public BadDeployParametersException(string message)
		: base(message)
	{
	}
}