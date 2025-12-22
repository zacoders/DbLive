namespace EasyFlow.Tests.Deployers;

public class EasyFlowTests
{
	[Fact]
	public void DropDatabase()
	{
		// Arrange
		MockSet mockSet = new();

		var easyFlow = mockSet.CreateUsingMocks<EasyFlow>();

		// Act
		easyFlow.DropDatabase();

		// Assert
		mockSet.EasyFlowDA.Received().DropDB();
	}

	[Fact]
	public void Deploy()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.EasyFlowInternal.SelfDeployProjectInternal();

		var easyFlow = mockSet.CreateUsingMocks<EasyFlow>();

		DeployParameters parameters = new()
		{
			CreateDbIfNotExists = true
		};

		// Act
		easyFlow.Deploy(parameters);

		// Assert
		mockSet.EasyFlowDA.Received().CreateDB();
		mockSet.EasyFlowInternal.Received().SelfDeployProjectInternal();
		mockSet.EasyFlowInternal.Received().DeployProjectInternal(Arg.Is(false), Arg.Is(parameters));
	}


	[Fact]
	public void Deploy_DoNotCreateDatabase()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.EasyFlowInternal.SelfDeployProjectInternal();

		var easyFlow = mockSet.CreateUsingMocks<EasyFlow>();

		DeployParameters parameters = new()
		{
			CreateDbIfNotExists = false
		};

		// Act
		easyFlow.Deploy(parameters);

		// Assert
		mockSet.EasyFlowDA.DidNotReceive().CreateDB();
		mockSet.EasyFlowInternal.Received().SelfDeployProjectInternal();
		mockSet.EasyFlowInternal.Received().DeployProjectInternal(Arg.Is(false), Arg.Is(parameters));
	}
}