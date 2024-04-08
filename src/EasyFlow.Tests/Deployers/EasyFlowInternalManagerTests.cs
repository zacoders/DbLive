namespace EasyFlow.Tests.Deployers;

public class EasyFlowInternalManagerTests
{
	[Fact]
	public void CreateEasyFlowInternal()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.EasyFlowBuilder.CloneBuilder().Returns(mockSet.EasyFlowBuilder);

		var easyFlow = mockSet.CreateUsingMocks<EasyFlowInternalManager>();

		// Act
		_ = easyFlow.CreateEasyFlowInternal();

		// Assert
		mockSet.EasyFlowBuilder.Received().CloneBuilder();
		//mockSet.EasyFlowBuilder.Received().SetProjectPath(Arg.Any<string>());
		mockSet.EasyFlowPaths.Received().GetPathToEasyFlowSelfProject();
	}
}