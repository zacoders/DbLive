namespace DbLive.Tests.Deployers.Migrations;

public class MigrationVersionDeployerTests
{
	[Fact]
	public async Task DeployMigration_EmptyMigration()
	{
		var mockSet = new MockSet();

		MigrationVersionDeployer deploy = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

		Migration migration = new()
		{
			Version = 1,
			Items = []
		};

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => deploy.DeployAsync(migration)
		);
	}

	[Fact]
	public async Task DeployMigration_WithoutMigrationItem()
	{
		var mockSet = new MockSet();

		MigrationVersionDeployer deploy = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

		Migration migration = new()
		{
			Version = 1,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Breaking] = new()
				{
					MigrationItemType = MigrationItemType.Breaking,
					FileData = GetFileData("001.breaking.sql")
				}
			}
		};

		await Assert.ThrowsAsync<InternalException>(
			() => deploy.DeployAsync(migration)
		);
	}

	[Fact]
	public async Task DeployMigration()
	{
		MockSet mockSet = new();

		MigrationVersionDeployer deploy = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

		Migration migration = new()
		{
			Version = 1,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Migration] = new()
				{
					MigrationItemType = MigrationItemType.Migration,
					FileData = GetFileData("item1.sql")
				}
			}
		};

		await deploy.DeployAsync(migration);

		await mockSet.MigrationItemDeployer.Received(1)
			.DeployAsync(1, Arg.Any<MigrationItem>(), Arg.Any<MigrationItemSettings>());
	}

	[Fact]
	public async Task DeployMigration_DeploysOnlyTheMigrationScript()
	{
		MockSet mockSet = new();

		MigrationVersionDeployer deploy = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

		Migration migration = new()
		{
			Version = 1,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Migration] = new()
				{
					MigrationItemType = MigrationItemType.Migration,
					FileData = GetFileData("item1.sql")
				},
				[MigrationItemType.Breaking] = new()
				{
					MigrationItemType = MigrationItemType.Breaking,
					FileData = GetFileData("breaking.sql")
				}
			}
		};

		await deploy.DeployAsync(migration);

		// the breaking script runs in the breaking phase.
		await mockSet.MigrationItemDeployer.Received(1)
			.DeployAsync(Arg.Any<long>(), Arg.Any<MigrationItem>(), Arg.Any<MigrationItemSettings>());
		await mockSet.MigrationItemDeployer.Received(1)
			.DeployAsync(1, migration.Items[MigrationItemType.Migration], Arg.Any<MigrationItemSettings>());

		// the schema checks belong to the breaking phase of the deployment tests.
		await mockSet.DbLiveDA.DidNotReceive().GetSchemaSnapshotAsync();
	}

	private static FileData GetFileData(string relativePath, string content = "-- default item content")
	{
		return new FileData
		{
			Content = content,
			RelativePath = relativePath
		};
	}


	[Fact]
	public async Task DeployMigration_WithCustomSettings()
	{
		MockSet mockSet = new();

		MigrationVersionDeployer deploy = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

		string settingsJson = """
		{
		  "TransactionWrapLevel": "none",
		  "TransactionIsolationLevel": "ReadCommitted",
		  "Timeout": "02:30:00"
		}
		""";

		Migration migration = new()
		{
			Version = 1,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Migration] = new()
				{
					MigrationItemType = MigrationItemType.Migration,
					FileData = GetFileData("item1.sql")
				},
				[MigrationItemType.MigrationSettings] = new()
				{
					MigrationItemType = MigrationItemType.MigrationSettings,
					FileData = GetFileData("custom-settings.json", settingsJson)
				}
			}
		};

		await deploy.DeployAsync(migration);

		await mockSet.MigrationItemDeployer.Received(1)
			.DeployAsync(
				1,
				migration.Items[MigrationItemType.Migration],
				new MigrationItemSettings
				{
					TransactionWrapLevel = TransactionWrapLevel.None,
					TransactionIsolationLevel = TranIsolationLevel.ReadCommitted,
					Timeout = TimeSpan.FromHours(2.5)
				}
			);

		// the transaction is opened by the item deployer, per item.
		await mockSet.TransactionRunner.DidNotReceiveWithAnyArgs()
			.ExecuteWithinTransactionAsync(default, default, default, default!);
	}
}
