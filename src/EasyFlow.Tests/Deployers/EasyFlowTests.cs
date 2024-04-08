namespace EasyFlow.Tests.Deployers;

public class EasyFlowTests
{
	[Fact]
	public void RunTest()
	{
		// Arrange
		MockSet mockSet = new();

		var easyFlow = mockSet.CreateUsingMocks<EasyFlow>();

		// Act
		easyFlow.DropDatabase();

		// Assert
		mockSet.EasyFlowDA.Received().DropDB();
	}
}