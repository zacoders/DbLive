using EasyFlow;
using EasyFlow.Common;
using Xunit;

namespace DemoMSSQL.Tests;

public class EasyFlowTestingMSSQLTheoryData : TheoryData<string>
{
	public EasyFlowTestingMSSQLTheoryData(string projectPath)
	{
		var project = new EasyFlowBuilder()
			.SetProjectPath(projectPath)
			.CreateProject();

		foreach (var testItem in project.GetTests())
		{
			Add(testItem.FileData.RelativePath); // adding tests to TheoryData base class.
		}
	}
}
