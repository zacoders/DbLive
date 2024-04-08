namespace EasyFlow.Tests.Deployers.Migrations;

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