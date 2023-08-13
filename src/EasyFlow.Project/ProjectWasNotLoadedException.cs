namespace EasyFlow.Project;

[Serializable]
internal class ProjectWasNotLoadedException : Exception
{
	public ProjectWasNotLoadedException()
		: base("Project was not loaded.")
	{
	}
}