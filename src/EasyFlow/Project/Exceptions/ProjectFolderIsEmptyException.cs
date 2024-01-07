namespace EasyFlow.Project.Exceptions;


[ExcludeFromCodeCoverage]
[Serializable]
public class ProjectFolderIsEmptyException(string path)
	: Exception($"Project folder is empty {path}")
{
}