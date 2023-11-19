namespace EasyFlow.Project;


[ExcludeFromCodeCoverage]
[Serializable]
public class ProjectWasNotLoadedException : Exception
{
	public ProjectWasNotLoadedException()
		: base("Project was not loaded.")
	{
	}
}