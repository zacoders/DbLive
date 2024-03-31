using EasyFlow.Adapter;
using EasyFlow.Deployers.Migrations;

namespace EasyFlow.Tests.Deployers.Migrations;

public class BreakingChangesDeployerTests
{
    //static Migration NewMigration(int version, string name) =>
    //    new()
    //    {
    //        Version = version,
    //        Name = name,
    //        FolderPath = "c:/",
    //        Items = new List<MigrationItem>().AsReadOnly()
    //    };

    [Fact]
    public void GetMigrationsToApply_SkipBreakingChangesDeployment()
    {
        // Arrange
        MockSet mockSet = new();

		var deploy = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

        // Act
        deploy.DeployBreakingChanges(new DeployParameters { DeployBreaking = false });

        // Assert
        mockSet.EasyFlowDA.DidNotReceive().GetNonAppliedBreakingMigrationItems();
	}


	[Fact]
	public void GetMigrationsToApply_NoBreaknigChangesItemssToApply()
	{
		// Arrange
		MockSet mockSet = new();

        mockSet.EasyFlowDA.GetNonAppliedBreakingMigrationItems()
            .Returns(new List<MigrationItemDto>().AsReadOnly());

		var deploy = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

        // Act
        deploy.DeployBreakingChanges(DeployParameters.Default);

        // Assert
        mockSet.EasyFlowProject.DidNotReceive().GetMigrations();
	}


	[Fact]
	public void GetMigrationsToApply()
	{
		// Arrange
		MockSet mockSet = new();

        mockSet.EasyFlowDA.GetNonAppliedBreakingMigrationItems()
            .Returns(new List<MigrationItemDto>
            {
				new() {
					Version = 2,
					ContentHash = 123,
					Content = "-- content 1",
					ItemType = "breaking",
					Name = "breaking1",
					Status = "skipped"
				},
				new() {
					Version = 2,
					ContentHash = 123,
					Content = "-- content 2",
					ItemType = "breaking",
					Name = "breaking2",
					Status = "skipped"
				},
				new() {
					Version = 3,
					ContentHash = 123,
					Content = "-- content 333",
					ItemType = "breaking",
					Name = "breaking1",
					Status = "skipped"
				}
			}.AsReadOnly()
        );

		var deploy = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		// Act
		deploy.DeployBreakingChanges(DeployParameters.Default);

		// Assert
		mockSet.EasyFlowProject.DidNotReceive().GetMigrations();
	}

}