namespace EasyFlow.Exceptions;

public class BadDeployParametersException : Exception
{
    public BadDeployParametersException(string message)
        : base(message)
    {
    }
}