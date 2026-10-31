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
			() => deploy.DeployAsync(migration, DeployParameters.Default)
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

		await deploy.DeployAsync(migration, DeployParameters.Default);

		await mockSet.MigrationItemDeployer.Received(1)
			.DeployAsync(1, Arg.Any<MigrationItem>(), Arg.Any<MigrationItemSettings>());
	}

	[Fact]
	public async Task DeployMigration_SkipMigrationTypes()
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
				[MigrationItemType.Undo] = new()
				{
					MigrationItemType = MigrationItemType.Undo,
					FileData = GetFileData("undo.sql")
				},
				[MigrationItemType.Breaking] = new()
				{
					MigrationItemType = MigrationItemType.Breaking,
					FileData = GetFileData("breaking.sql")
				}
			}
		};

		await deploy.DeployAsync(migration, DeployParameters.Default);

		await mockSet.MigrationItemDeployer.Received()
			.DeployAsync(1, Arg.Any<MigrationItem>(), Arg.Any<MigrationItemSettings>());
	}

	private static FileData GetFileData(string relativePath, string content = "-- default item content")
	{
		return new FileData
		{
			Content = content,
			RelativePath = relativePath,
			FilePath = "c:/data" + relativePath
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

		await deploy.DeployAsync(migration, DeployParameters.Default);

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

	[Fact]
	public async Task UndoTestMode_deploys_each_item_by_its_own_settings()
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
					FileData = GetFileData("001.migration.sql")
				},
				[MigrationItemType.MigrationSettings] = new()
				{
					MigrationItemType = MigrationItemType.MigrationSettings,
					FileData = GetFileData("001.migration.settings.json", """{ "TransactionWrapLevel": "None" }""")
				},
				[MigrationItemType.Breaking] = new()
				{
					MigrationItemType = MigrationItemType.Breaking,
					FileData = GetFileData("001.breaking.sql")
				},
				[MigrationItemType.BreakingSettings] = new()
				{
					MigrationItemType = MigrationItemType.BreakingSettings,
					FileData = GetFileData("001.breaking.settings.json", """{ "TransactionIsolationLevel": "Serializable" }""")
				},
				[MigrationItemType.Undo] = new()
				{
					MigrationItemType = MigrationItemType.Undo,
					FileData = GetFileData("001.undo.sql")
				},
				[MigrationItemType.UndoSettings] = new()
				{
					MigrationItemType = MigrationItemType.UndoSettings,
					FileData = GetFileData("001.undo.settings.json", """{ "Timeout": "00:05:00" }""")
				}
			}
		};

		DeployParameters parameters = DeployParameters.Default with
		{
			UndoTestDeployment = UndoTestMode.MigrationBreakingUndoMigration
		};

		await deploy.DeployAsync(migration, parameters);

		MigrationItemSettings migrationSettings = new() { TransactionWrapLevel = TransactionWrapLevel.None };
		MigrationItemSettings breakingSettings = new() { TransactionIsolationLevel = TranIsolationLevel.Serializable };
		MigrationItemSettings undoSettings = new() { Timeout = TimeSpan.FromMinutes(5) };

		Received.InOrder(() =>
		{
			mockSet.MigrationItemDeployer.DeployAsync(1, migration.Items[MigrationItemType.Migration], migrationSettings);
			mockSet.MigrationItemDeployer.DeployAsync(1, migration.Items[MigrationItemType.Breaking], breakingSettings);
			mockSet.MigrationItemDeployer.DeployAsync(1, migration.Items[MigrationItemType.Undo], undoSettings);
			mockSet.MigrationItemDeployer.DeployAsync(1, migration.Items[MigrationItemType.Migration], migrationSettings);
		});
	}
}
