namespace DbLive.Tests.Deployers;

public class DbLiveInternalManagerTests
{
	[Fact]
	public void CreateDbLiveInternal()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.DbLiveBuilder.CloneBuilder().Returns(mockSet.DbLiveBuilder);

		var DbLive = mockSet.CreateUsingMocks<DbLiveInternalManager>();

		// Act
		_ = DbLive.CreateDbLiveInternal();

		// Assert
		mockSet.DbLiveBuilder.Received().CloneBuilder();
		//mockSet.DbLiveBuilder.Received().SetProjectPath(Arg.Any<string>());
		mockSet.DbLivePaths.Received().GetPathToDbLiveSelfProject();
	}
}