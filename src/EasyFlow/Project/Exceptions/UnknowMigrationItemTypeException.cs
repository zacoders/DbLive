namespace EasyFlow.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class UnknowMigrationItemTypeException : Exception
{
	public static string AllowedItemTypes = string.Join(", ", Enum.GetValues(typeof(MigrationItemType)));

	public UnknowMigrationItemTypeException(string unknownItemType)
		: base($"Unknown migration item type '{unknownItemType}'. Allowed values: {AllowedItemTypes}")
	{
	}
}