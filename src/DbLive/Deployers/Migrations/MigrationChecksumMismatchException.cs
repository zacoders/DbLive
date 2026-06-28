namespace DbLive.Deployers.Migrations;

[ExcludeFromCodeCoverage]
public class MigrationChecksumMismatchException(string errorMessage)
	: Exception(errorMessage)
{
}
