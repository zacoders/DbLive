
namespace DbLive.Deployers.Testing;

[Serializable]
[ExcludeFromCodeCoverage]
internal class UnitTestsFailedException : Exception
{
	public UnitTestsFailedException()
	{
	}

	public UnitTestsFailedException(string? message) : base(message)
	{
	}

	public UnitTestsFailedException(string? message, Exception? innerException) : base(message, innerException)
	{
	}
}