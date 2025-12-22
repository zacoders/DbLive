namespace EasyFlow.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class ProjectFolderIsEmptyException(string path)
	: Exception($"Project folder is empty {path}")
{
}