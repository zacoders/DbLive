namespace DbLive.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class UnknownMigrationItemTypeException : Exception
{
	public static string AllowedItemTypes = string.Join(", ", Enum.GetNames(typeof(MigrationItemType)));

	public UnknownMigrationItemTypeException(string unknownItemType, string fileName)
		: base($"Unknown migration item type '{unknownItemType}' in {fileName}. Allowed values: {AllowedItemTypes}")
	{
	}
}