namespace EasyFlow;

[Serializable]
public class UnknowMigrationTaskTypeException : Exception
{
	public static string AllowedMigrationTasks = string.Join(", ", Enum.GetValues(typeof(MigrationType)));

	public UnknowMigrationTaskTypeException(string unknownTaskType)
		: base($"Unknown task type '{unknownTaskType}'. Allowed values: {AllowedMigrationTasks}")
	{
	}
}