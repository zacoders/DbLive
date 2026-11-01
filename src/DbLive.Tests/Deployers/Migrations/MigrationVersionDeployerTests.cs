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
			UndoTestDeployment = UndoTestMode.UndoCycle
		};

		await deploy.DeployAsync(migration, parameters);

		MigrationItemSettings migrationSettings = new() { TransactionWrapLevel = TransactionWrapLevel.None };
		MigrationItemSettings breakingSettings = new() { TransactionIsolationLevel = TranIsolationLevel.Serializable };
		MigrationItemSettings undoSettings = new() { Timeout = TimeSpan.FromMinutes(5) };

		Received.InOrder(() =>
		{
			mockSet.MigrationItemDeployer.DeployAsync(1, migration.Items[MigrationItemType.Migration], migrationSettings);
			mockSet.MigrationItemDeployer.DeployAsync(1, migration.Items[MigrationItemType.Undo], undoSettings);
			mockSet.MigrationItemDeployer.DeployAsync(1, migration.Items[MigrationItemType.Migration], migrationSettings);
			mockSet.MigrationItemDeployer.DeployAsync(1, migration.Items[MigrationItemType.Breaking], breakingSettings);
			mockSet.MigrationItemDeployer.DeployAsync(1, migration.Items[MigrationItemType.Breaking], breakingSettings);
			mockSet.MigrationItemDeployer.DeployAsync(1, migration.Items[MigrationItemType.Undo], undoSettings);
			mockSet.MigrationItemDeployer.DeployAsync(1, migration.Items[MigrationItemType.Migration], migrationSettings);
		});
	}

	private static readonly SchemaSnapshot EmptySchema = new([]);
	private static readonly SchemaSnapshot SchemaWithPhone = new([new SchemaObject(SchemaObjectType.Column, "dbo.Person.Phone", "varchar(20) null", "dbo.Person")]);

	[Fact]
	public async Task Without_undo_test_mode_no_schema_snapshot()
	{
		MockSet mockSet = new();
		MigrationVersionDeployer deploy = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

		await deploy.DeployAsync(GetMigrationWithUndo(withBreaking: true), DeployParameters.Default);

		await mockSet.DbLiveDA.DidNotReceive().GetSchemaSnapshotAsync();
	}

	[Theory]
	[InlineData(false, 2, 3)]
	[InlineData(true, 5, 7)]
	public async Task Undo_that_restores_the_schema_passes(bool withBreaking, int snapshots, int deployments)
	{
		MockSet mockSet = new();
		MigrationVersionDeployer deploy = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

		// every snapshot: other instances with the same objects.
		mockSet.DbLiveDA.GetSchemaSnapshotAsync().Returns(_ => new SchemaSnapshot(SchemaWithPhone.Objects));

		await deploy.DeployAsync(GetMigrationWithUndo(withBreaking), DeployParameters.Default with { UndoTestDeployment = UndoTestMode.UndoCycle });

		await mockSet.DbLiveDA.Received(snapshots).GetSchemaSnapshotAsync();
		await mockSet.MigrationItemDeployer.Received(deployments).DeployAsync(1, Arg.Any<MigrationItem>(), Arg.Any<MigrationItemSettings>());
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Undo_that_does_not_restore_the_schema_before_the_breaking_script_fails(bool withBreaking)
	{
		MockSet mockSet = new();
		MigrationVersionDeployer deploy = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

		mockSet.DbLiveDA.GetSchemaSnapshotAsync().Returns(SchemaWithPhone, EmptySchema);

		SchemaChangedException ex = await Assert.ThrowsAsync<SchemaChangedException>(
			() => deploy.DeployAsync(GetMigrationWithUndo(withBreaking), DeployParameters.Default with { UndoTestDeployment = UndoTestMode.UndoCycle })
		);

		Assert.Equal(SchemaCheck.Undo, ex.Check);
		Assert.Equal(
			"""
			Undo test: 001.undo.sql does not restore the schema. The schema after the undo differs from the schema before 001.migration.sql:
			  missing column dbo.Person.Phone: varchar(20) null
			""".ReplaceLineEndings().TrimEnd(),
			ex.Message
		);

		// neither the breaking script nor the migration is deployed again.
		await mockSet.MigrationItemDeployer.Received(2).DeployAsync(1, Arg.Any<MigrationItem>(), Arg.Any<MigrationItemSettings>());
	}

	[Fact]
	public async Task Breaking_script_whose_second_run_changes_the_schema_fails()
	{
		MockSet mockSet = new();
		MigrationVersionDeployer deploy = mockSet.CreateUsingMocks<MigrationVersionDeployer>();
		Migration migration = GetMigrationWithUndo(withBreaking: true);

		// before the migration, after the first undo, after the first breaking run, after the second one.
		mockSet.DbLiveDA.GetSchemaSnapshotAsync().Returns(SchemaWithPhone, SchemaWithPhone, SchemaWithPhone, EmptySchema);

		SchemaChangedException ex = await Assert.ThrowsAsync<SchemaChangedException>(
			() => deploy.DeployAsync(migration, DeployParameters.Default with { UndoTestDeployment = UndoTestMode.UndoCycle })
		);

		Assert.Equal(SchemaCheck.BreakingSecondRun, ex.Check);
		Assert.StartsWith("Undo test: the breaking script 001.breaking.sql is not idempotent, its second run changed the schema:", ex.Message);
		Assert.Contains("missing column dbo.Person.Phone", ex.Message);

		// the undo runs only in the cycle before the breaking script.
		await mockSet.MigrationItemDeployer.Received(1).DeployAsync(1, migration.Items[MigrationItemType.Undo], Arg.Any<MigrationItemSettings>());
	}

	[Fact]
	public async Task Undo_after_two_breaking_runs_is_compared_with_the_schema_before_the_migration()
	{
		MockSet mockSet = new();
		MigrationVersionDeployer deploy = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

		// the undo before the breaking script restores the schema; the breaking runs drop the column, the undo after them does not add it back.
		mockSet.DbLiveDA.GetSchemaSnapshotAsync().Returns(SchemaWithPhone, SchemaWithPhone, EmptySchema, EmptySchema, EmptySchema);

		SchemaChangedException ex = await Assert.ThrowsAsync<SchemaChangedException>(
			() => deploy.DeployAsync(GetMigrationWithUndo(withBreaking: true), DeployParameters.Default with { UndoTestDeployment = UndoTestMode.UndoCycle })
		);

		Assert.Equal(SchemaCheck.Undo, ex.Check);
		Assert.Equal(
			"""
			Undo test: 001.undo.sql does not restore the schema after the breaking script 001.breaking.sql. The schema after the undo differs from the schema before 001.migration.sql:
			  missing column dbo.Person.Phone: varchar(20) null
			""".ReplaceLineEndings().TrimEnd(),
			ex.Message
		);
		await mockSet.DbLiveDA.Received(5).GetSchemaSnapshotAsync();

		// the migration is not deployed again.
		await mockSet.MigrationItemDeployer.Received(6).DeployAsync(1, Arg.Any<MigrationItem>(), Arg.Any<MigrationItemSettings>());
	}

	private static Migration GetMigrationWithUndo(bool withBreaking)
	{
		Dictionary<MigrationItemType, MigrationItem> items = new()
		{
			[MigrationItemType.Migration] = new()
			{
				MigrationItemType = MigrationItemType.Migration,
				FileData = GetFileData("001.migration.sql")
			},
			[MigrationItemType.Undo] = new()
			{
				MigrationItemType = MigrationItemType.Undo,
				FileData = GetFileData("001.undo.sql")
			}
		};

		if (withBreaking)
		{
			items[MigrationItemType.Breaking] = new()
			{
				MigrationItemType = MigrationItemType.Breaking,
				FileData = GetFileData("001.breaking.sql")
			};
		}

		return new Migration
		{
			Version = 1,
			Items = items
		};
	}
}
