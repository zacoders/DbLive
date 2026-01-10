namespace DbLive.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class UnknownMigrationItemTypeException(string unknownItemType, string fileName)
	: Exception($"Unknown migration item type '{unknownItemType}' in {fileName}. Allowed values: {AllowedItemTypes}")
{
	public static string AllowedItemTypes = string.Join(", ", Enum.GetNames(typeof(MigrationItemType)));
}