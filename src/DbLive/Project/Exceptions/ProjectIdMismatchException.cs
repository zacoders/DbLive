namespace DbLive;

[ExcludeFromCodeCoverage]
public sealed class ProjectIdMismatchException : Exception
{
	internal ProjectIdMismatchException(string message)
		: base(message)
	{
	}
}
