namespace DbLive.Common;

public interface IProjectPathAccessor
{
	string ProjectPath { get; }
	string VisualStudioProjectPath { get; }
}
