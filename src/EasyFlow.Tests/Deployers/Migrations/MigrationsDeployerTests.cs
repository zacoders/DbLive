using EasyFlow.Adapter;
using EasyFlow.Deployers.Migrations;

namespace EasyFlow.Tests.Deployers.Migrations;

public class MigrationsDeployerTests
{
    static Migration NewMigration(int version, string name) =>
        new()
        {
            Version = version,
            Name = name,
            FolderPath = "c:/",
            Items = new List<MigrationItem>().AsReadOnly()
        };

    [Fact]
    public void GetMigrationsToApply()
    {
        var mockSet = new MockSet();

        var deploy = new MigrationsDeployer(mockSet.Logger, mockSet.EasyFlowProject, mockSet.EasyFlowDA, mockSet.MigrationDeployer);

        mockSet.EasyFlowProject.GetMigrations()
            .Returns(new[]
            {
                NewMigration(1, "test1"),
                NewMigration(2, "same-version-1"),
                NewMigration(2, "same-version-2"),
                NewMigration(3, "test3")
            });

        mockSet.EasyFlowDA.EasyFlowInstalled().Returns(true);

        mockSet.EasyFlowDA.GetMigrations()
            .Returns(new[]
            {
                new MigrationDto { Version = 1, Name = "test1" },
                new MigrationDto { Version = 2, Name = "same-version-2" }
            });


        var migrations = deploy.GetMigrationsToApply(false, DeployParameters.Default).ToArray();

        Assert.Equal(2, migrations.Length);

        Assert.Equal(2, migrations[0].Version);
        Assert.Equal("same-version-1", migrations[0].Name);

        Assert.Equal(3, migrations[1].Version);
        Assert.Equal("test3", migrations[1].Name);
    }


    [Fact]
    public void GetMigrationsToApply_MaxVersionToDeploy_Specified()
    {
        var mockSet = new MockSet();

        var deploy = new MigrationsDeployer(mockSet.Logger, mockSet.EasyFlowProject, mockSet.EasyFlowDA, mockSet.MigrationDeployer);

        mockSet.EasyFlowProject.GetMigrations()
            .Returns(new[]
            {
                NewMigration(1, "test1"),
                NewMigration(2, "same-version-1"),
                NewMigration(2, "same-version-2"),
                NewMigration(3, "test3")
            });

        mockSet.EasyFlowDA.EasyFlowInstalled().Returns(true);

        mockSet.EasyFlowDA.GetMigrations()
            .Returns(new[]
            {
                new MigrationDto { Version = 1, Name = "test1" },
                new MigrationDto { Version = 2, Name = "same-version-2" }
            });

        var deployParams = DeployParameters.Default;
        deployParams.MaxVersionToDeploy = 2;

        var migrations = deploy.GetMigrationsToApply(false, deployParams).ToArray();

        Assert.Single(migrations);

        Assert.Equal(2, migrations[0].Version);
        Assert.Equal("same-version-1", migrations[0].Name);
    }

    [Fact]
    public void GetMigrationsToApply_SelfDeploy_EasyFlowIsNotInstalled()
    {
        var mockSet = new MockSet();

        var deploy = new MigrationsDeployer(mockSet.Logger, mockSet.EasyFlowProject, mockSet.EasyFlowDA, mockSet.MigrationDeployer);

        mockSet.EasyFlowProject.GetMigrations()
            .Returns(new[]
            {
                NewMigration(1, "test1"),
                NewMigration(2, "same-version-1"),
                NewMigration(2, "same-version-2"),
                NewMigration(3, "test3")
            });

        mockSet.EasyFlowDA.EasyFlowInstalled().Returns(false);
        mockSet.EasyFlowDA.GetEasyFlowVersion().Returns(1);

        var migrations = deploy.GetMigrationsToApply(true, DeployParameters.Default).ToArray();

        Assert.Equal(4, migrations.Length);
    }

    [Fact]
    public void GetMigrationsToApply_SelfDeploy_EasyFlowInstalled()
    {
        var mockSet = new MockSet();

        var deploy = new MigrationsDeployer(mockSet.Logger, mockSet.EasyFlowProject, mockSet.EasyFlowDA, mockSet.MigrationDeployer);

        mockSet.EasyFlowProject.GetMigrations()
            .Returns(new[]
            {
                NewMigration(1, "test1"),
                NewMigration(2, "same-version-1"),
                NewMigration(2, "same-version-2"),
                NewMigration(3, "test3")
            });

        mockSet.EasyFlowDA.EasyFlowInstalled().Returns(true);
        mockSet.EasyFlowDA.GetEasyFlowVersion().Returns(1);

        var migrations = deploy.GetMigrationsToApply(true, DeployParameters.Default).ToArray();

        Assert.Equal(3, migrations.Length);

        Assert.Equal(2, migrations[0].Version);
        Assert.Equal("same-version-1", migrations[0].Name);

        Assert.Equal(2, migrations[1].Version);
        Assert.Equal("same-version-2", migrations[1].Name);

        Assert.Equal(3, migrations[2].Version);
        Assert.Equal("test3", migrations[2].Name);
    }


    [Fact]
    public void GetMigrationsToApply_SelfDeploy_EasyFlowInstalled_MaxVersionToDeploySpecified()
    {
        var mockSet = new MockSet();

        var deploy = new MigrationsDeployer(mockSet.Logger, mockSet.EasyFlowProject, mockSet.EasyFlowDA, mockSet.MigrationDeployer);

        mockSet.EasyFlowProject.GetMigrations()
            .Returns(new[]
            {
                NewMigration(1, "test1"),
                NewMigration(2, "same-version-1"),
                NewMigration(2, "same-version-2"),
                NewMigration(3, "test3")
            });

        mockSet.EasyFlowDA.EasyFlowInstalled().Returns(true);
        mockSet.EasyFlowDA.GetEasyFlowVersion().Returns(1);

        var deployParams = DeployParameters.Default;
        deployParams.MaxVersionToDeploy = 2;

        var migrations = deploy.GetMigrationsToApply(true, deployParams).ToArray();

        Assert.Equal(2, migrations.Length);

        Assert.Equal(2, migrations[0].Version);
        Assert.Equal("same-version-1", migrations[0].Name);

        Assert.Equal(2, migrations[1].Version);
        Assert.Equal("same-version-2", migrations[1].Name);
    }
}