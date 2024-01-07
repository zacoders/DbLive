namespace EasyFlow.Project.Exceptions;


[ExcludeFromCodeCoverage]
[Serializable]
public class UnknowMigrationTaskTypeException : Exception
{
    public static string AllowedMigrationTasks = string.Join(", ", Enum.GetValues(typeof(MigrationItemType)));

    public UnknowMigrationTaskTypeException(string unknownTaskType)
        : base($"Unknown task type '{unknownTaskType}'. Allowed values: {AllowedMigrationTasks}")
    {
    }
}