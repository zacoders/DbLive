
namespace DbLive.Project.Exceptions;

public class InvalidMigrationItemTypeException : Exception
{
	public InvalidMigrationItemTypeException(
		string fileName,
		MigrationItemType migrationItemType,
		string fileExtension,
		string expectedFileExtension
	)
		: base($"Invalid migration item type for file '{fileName}': Migration item type '{migrationItemType}' cannot have extension '{fileExtension}'. Expected extension is '{expectedFileExtension}'.")
	{
	}
}