namespace DbLive;

[ExcludeFromCodeCoverage]
public sealed class MigrationChecksumMismatchException : Exception
{
	internal MigrationChecksumMismatchException(string errorMessage)
		: base(errorMessage)
	{
	}
}
