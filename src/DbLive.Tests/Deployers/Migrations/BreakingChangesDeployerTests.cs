namespace DbLive.Tests.Deployers.Migrations;

public class BreakingChangesDeployerTests
{
	[Fact]
	public async Task Does_nothing_when_deploy_breaking_is_disabled()
	{
		// Arrange
		MockSet mockSet = new();

		BreakingChangesDeployer deployer = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		// Act
		await deployer.DeployAsync(new DeployParameters { DeployBreaking = false });

		// Assert
		await mockSet.DbLiveDA.DidNotReceive().GetMigrationsAsync();
		await mockSet.DbLiveProject.DidNotReceive().GetMigrationsAsync();
		await mockSet.MigrationItemDeployer.DidNotReceive()
			.DeployAsync(Arg.Any<long>(), Arg.Any<MigrationItem>(), Arg.Any<MigrationItemSettings>());
	}

	[Fact]
	public async Task Logs_and_returns_when_no_breaking_items_exist()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.DbLiveDA.GetMigrationsAsync().Returns([]);

		mockSet.DbLiveProject.GetMigrationsAsync().Returns([
			new Migration
		{
			Version = 1,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Migration] = new()
				{
					MigrationItemType = MigrationItemType.Migration,
					FileData = GetFileData("001.m.sql", "-- content")
				}
			}
		}
		]);

		BreakingChangesDeployer deployer = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		// Act
		await deployer.DeployAsync(DeployParameters.Breaking);

		// Assert
		await mockSet.MigrationItemDeployer.DidNotReceive()
			.DeployAsync(Arg.Any<long>(), Arg.Any<MigrationItem>(), Arg.Any<MigrationItemSettings>());
	}

	[Fact]
	public async Task Applies_only_pending_breaking_for_applied_migrations()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.DbLiveDA.GetMigrationsAsync().Returns([
			new MigrationItemDto
			{
				Version = 2,
				ItemType = MigrationItemType.Migration,
				Status = MigrationItemStatus.Applied,
				Name = "migration",
				RelativePath = "migrations/002.migration.sql",
				ContentHash = 222
			},
			new MigrationItemDto
			{
				Version = 2,
				Name = "breaking",
				ItemType = MigrationItemType.Breaking,
				Status = MigrationItemStatus.Applied,
				RelativePath = "migrations/002.breaking.sql",
				ContentHash = 2222
			},
			new MigrationItemDto
			{
				Version = 3,
				ItemType = MigrationItemType.Migration,
				Status = MigrationItemStatus.Applied,
				Name = "migration",
				RelativePath = "migrations/003.migration.sql",
				ContentHash = 333
			},
			new MigrationItemDto
			{
				Version = 3,
				Name = "breaking",
				ItemType = MigrationItemType.Breaking,
				Status = MigrationItemStatus.None,
				ContentHash = 3333,
				RelativePath = "migrations/003.breaking.sql"
			},
		]);

		Migration migration2 = new()
		{
			Version = 2,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Breaking] = new()
				{
					MigrationItemType = MigrationItemType.Breaking,
					FileData = GetFileData("2.breaking.sql", "-- content 2")
				}
			}
		};

		Migration migration3 = new()
		{
			Version = 3,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Breaking] = new()
				{
					MigrationItemType = MigrationItemType.Breaking,
					FileData = GetFileData("3.breaking.sql", "-- content 3")
				}
			}
		};

		mockSet.DbLiveProject.GetMigrationsAsync().Returns([migration2, migration3]);

		BreakingChangesDeployer deployer = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		// Act
		await deployer.DeployAsync(DeployParameters.Breaking);

		// Assert
		await mockSet.MigrationItemDeployer.Received(1)
			.DeployAsync(3, migration3.Items[MigrationItemType.Breaking], new MigrationItemSettings());

		await mockSet.MigrationItemDeployer.DidNotReceive()
			.DeployAsync(2, Arg.Any<MigrationItem>(), Arg.Any<MigrationItemSettings>());
	}

	[Fact]
	public async Task Applies_pending_breaking_in_version_order_and_skips_unapplied_migrations()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.DbLiveDA.GetMigrationsAsync().Returns([
			new MigrationItemDto
			{
				Version = 1,
				ItemType = MigrationItemType.Migration,
				Status = MigrationItemStatus.Applied,
				Name = "migration",
				RelativePath = "migrations/001.migration.sql",
				ContentHash = 111
			},
			new MigrationItemDto
			{
				Version = 1,
				ItemType = MigrationItemType.Breaking,
				Status = MigrationItemStatus.None,
				Name = "breaking",
				RelativePath = "migrations/001.breaking.sql",
				ContentHash = 1111
			},
			new MigrationItemDto
			{
				Version = 2,
				ItemType = MigrationItemType.Migration,
				Status = MigrationItemStatus.Applied,
				Name = "migration",
				RelativePath = "migrations/002.migration.sql",
				ContentHash = 222
			},
			new MigrationItemDto
			{
				Version = 2,
				ItemType = MigrationItemType.Breaking,
				Status = MigrationItemStatus.None,
				Name = "breaking",
				RelativePath = "migrations/002.breaking.sql",
				ContentHash = 2222
			},

			new MigrationItemDto
			{
				Version = 3,
				ItemType = MigrationItemType.Migration,
				Status = MigrationItemStatus.None,
				Name = "migration",
				RelativePath = "migrations/003.migration.sql",
				ContentHash = 333
			},
			new MigrationItemDto
			{
				Version = 3,
				ItemType = MigrationItemType.Breaking,
				Status = MigrationItemStatus.None,
				Name = "breaking",
				RelativePath = "migrations/003.breaking.sql",
				ContentHash = 3333
			},
		]);

		Migration migration1 = new()
		{
			Version = 1,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Breaking] = new()
				{
					MigrationItemType = MigrationItemType.Breaking,
					FileData = GetFileData("1.breaking.sql", "-- content 1")
				}
			}
		};

		Migration migration2 = new()
		{
			Version = 2,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Breaking] = new()
				{
					MigrationItemType = MigrationItemType.Breaking,
					FileData = GetFileData("2.breaking.sql", "-- content 2")
				}
			}
		};

		Migration migration3 = new()
		{
			Version = 3,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Breaking] = new()
				{
					MigrationItemType = MigrationItemType.Breaking,
					FileData = GetFileData("3.breaking.sql", "-- content 3")
				}
			}
		};

		mockSet.DbLiveProject.GetMigrationsAsync().Returns([migration1, migration2, migration3]);

		BreakingChangesDeployer deployer = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		// Act
		await deployer.DeployAsync(DeployParameters.Breaking);

		// Assert
		Received.InOrder(() =>
		{
			mockSet.MigrationItemDeployer
				.DeployAsync(1, migration1.Items[MigrationItemType.Breaking], new MigrationItemSettings());

			mockSet.MigrationItemDeployer
				.DeployAsync(2, migration2.Items[MigrationItemType.Breaking], new MigrationItemSettings());
		});

		await mockSet.MigrationItemDeployer.DidNotReceive()
			.DeployAsync(3, Arg.Any<MigrationItem>(), Arg.Any<MigrationItemSettings>());
	}

	[Fact]
	public async Task Applies_each_breaking_by_its_own_settings_file()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.DbLiveDA.GetMigrationsAsync().Returns([
			new MigrationItemDto
			{
				Version = 1,
				ItemType = MigrationItemType.Migration,
				Status = MigrationItemStatus.Applied,
				Name = "migration",
				RelativePath = "migrations/001.migration.sql",
				ContentHash = 111
			},
			new MigrationItemDto
			{
				Version = 1,
				ItemType = MigrationItemType.Breaking,
				Status = MigrationItemStatus.None,
				Name = "breaking",
				RelativePath = "migrations/001.breaking.sql",
				ContentHash = 1111
			},
			new MigrationItemDto
			{
				Version = 2,
				ItemType = MigrationItemType.Migration,
				Status = MigrationItemStatus.Applied,
				Name = "migration",
				RelativePath = "migrations/002.migration.sql",
				ContentHash = 222
			},
			new MigrationItemDto
			{
				Version = 2,
				ItemType = MigrationItemType.Breaking,
				Status = MigrationItemStatus.None,
				Name = "breaking",
				RelativePath = "migrations/002.breaking.sql",
				ContentHash = 2222
			}
		]);

		Migration migration1 = new()
		{
			Version = 1,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Breaking] = new()
				{
					MigrationItemType = MigrationItemType.Breaking,
					FileData = GetFileData("1.breaking.sql", "-- content 1")
				},
				[MigrationItemType.BreakingSettings] = new()
				{
					MigrationItemType = MigrationItemType.BreakingSettings,
					FileData = GetFileData("1.breaking.settings.json", """{ "TransactionIsolationLevel": "Serializable", "Timeout": "00:10:00" }""")
				},
				// settings of another item must not be used for the breaking script.
				[MigrationItemType.MigrationSettings] = new()
				{
					MigrationItemType = MigrationItemType.MigrationSettings,
					FileData = GetFileData("1.migration.settings.json", """{ "TransactionWrapLevel": "None" }""")
				}
			}
		};

		Migration migration2 = new()
		{
			Version = 2,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Breaking] = new()
				{
					MigrationItemType = MigrationItemType.Breaking,
					FileData = GetFileData("2.breaking.sql", "-- content 2")
				}
			}
		};

		mockSet.DbLiveProject.GetMigrationsAsync().Returns([migration1, migration2]);

		BreakingChangesDeployer deployer = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		// Act
		await deployer.DeployAsync(DeployParameters.Breaking);

		// Assert
		Received.InOrder(() =>
		{
			mockSet.MigrationItemDeployer.DeployAsync(
				1,
				migration1.Items[MigrationItemType.Breaking],
				new MigrationItemSettings { TransactionIsolationLevel = TranIsolationLevel.Serializable, Timeout = TimeSpan.FromMinutes(10) }
			);

			mockSet.MigrationItemDeployer.DeployAsync(
				2,
				migration2.Items[MigrationItemType.Breaking],
				new MigrationItemSettings()
			);
		});
	}

	[Fact]
	public async Task Breaking_row_of_a_file_deleted_from_the_project_is_skipped()
	{
		MockSet mockSet = new();

		// 001.breaking.sql was saved by a release and deleted from the project before its breaking phase.
		_ = ArrangePendingMigrationBreaking(mockSet);
		mockSet.DbLiveProject.GetMigrationsAsync().Returns([
			new Migration
			{
				Version = 1,
				Items = new Dictionary<MigrationItemType, MigrationItem>
				{
					[MigrationItemType.Migration] = new()
					{
						MigrationItemType = MigrationItemType.Migration,
						FileData = GetFileData("001.migration.sql", "-- migration 1")
					}
				}
			}
		]);

		BreakingChangesDeployer deployer = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		await deployer.DeployAsync(DeployParameters.Breaking);

		// the scripts come from the project, the row alone runs nothing.
		await mockSet.MigrationItemDeployer.DidNotReceiveWithAnyArgs().DeployAsync(default, default!, default!);
	}

	[Fact]
	public async Task Without_second_run_check_each_breaking_script_runs_once()
	{
		MockSet mockSet = new();

		Migration migration1 = ArrangePendingMigrationBreaking(mockSet);

		BreakingChangesDeployer deployer = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		await deployer.DeployAsync(DeployParameters.Breaking);

		await mockSet.MigrationItemDeployer.Received(1)
			.DeployAsync(1, migration1.Items[MigrationItemType.Breaking], Arg.Any<MigrationItemSettings>());
		await mockSet.DbLiveDA.DidNotReceive().GetSchemaSnapshotAsync();
	}

	[Fact]
	public async Task Second_run_check_runs_every_breaking_script_twice_before_the_next_one()
	{
		MockSet mockSet = new();

		(Migration migration1, Migration migration2) = ArrangeTwoPendingMigrationBreaking(mockSet);

		// every snapshot: other instances with the same objects.
		mockSet.DbLiveDA.GetSchemaSnapshotAsync().Returns(_ => new SchemaSnapshot(SchemaWithPhone.Objects));

		BreakingChangesDeployer deployer = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		await deployer.DeployAsync(DeployParameters.Breaking with { CheckBreakingSecondRun = true });

		MigrationItemSettings breaking2Settings = new() { TransactionWrapLevel = TransactionWrapLevel.None };

		Received.InOrder(() =>
		{
			mockSet.MigrationItemDeployer.DeployAsync(1, migration1.Items[MigrationItemType.Breaking], new MigrationItemSettings());
			mockSet.DbLiveDA.GetSchemaSnapshotAsync();
			mockSet.MigrationItemDeployer.DeployAsync(1, migration1.Items[MigrationItemType.Breaking], new MigrationItemSettings());
			mockSet.DbLiveDA.GetSchemaSnapshotAsync();
			// each run by the settings of its script.
			mockSet.MigrationItemDeployer.DeployAsync(2, migration2.Items[MigrationItemType.Breaking], breaking2Settings);
			mockSet.DbLiveDA.GetSchemaSnapshotAsync();
			mockSet.MigrationItemDeployer.DeployAsync(2, migration2.Items[MigrationItemType.Breaking], breaking2Settings);
			mockSet.DbLiveDA.GetSchemaSnapshotAsync();
		});
		await mockSet.MigrationItemDeployer.ReceivedWithAnyArgs(4).DeployAsync(default, default!, default!);
	}

	[Fact]
	public async Task Breaking_script_whose_second_run_changes_the_schema_fails()
	{
		MockSet mockSet = new();

		(Migration migration1, _) = ArrangeTwoPendingMigrationBreaking(mockSet);

		// after the first run of 1.breaking.sql, after its second run.
		mockSet.DbLiveDA.GetSchemaSnapshotAsync().Returns(SchemaWithPhone, EmptySchema);

		BreakingChangesDeployer deployer = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		SchemaChangedException ex = await Assert.ThrowsAsync<SchemaChangedException>(
			() => deployer.DeployAsync(DeployParameters.Breaking with { CheckBreakingSecondRun = true })
		);

		Assert.Equal(SchemaCheck.BreakingSecondRun, ex.Check);
		Assert.Equal(
			"""
			Deployment test: the breaking script 1.breaking.sql is not idempotent, its second run changed the schema:
			  missing column dbo.Person.Phone: varchar(20) null
			""".ReplaceLineEndings().TrimEnd(),
			ex.Message
		);

		// the breaking script of the next version does not run.
		await mockSet.MigrationItemDeployer.Received(2)
			.DeployAsync(1, migration1.Items[MigrationItemType.Breaking], Arg.Any<MigrationItemSettings>());
		await mockSet.MigrationItemDeployer.DidNotReceive()
			.DeployAsync(2, Arg.Any<MigrationItem>(), Arg.Any<MigrationItemSettings>());
	}

	private static readonly SchemaSnapshot EmptySchema = new([]);
	private static readonly SchemaSnapshot SchemaWithPhone = new([new SchemaObject(SchemaObjectType.Column, "dbo.Person.Phone", "varchar(20) null", "dbo.Person")]);

	[Fact]
	public async Task Does_not_check_code_and_runs_no_code_breaking_scripts_when_deploy_breaking_is_disabled()
	{
		MockSet mockSet = new();

		BreakingChangesDeployer deployer = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		await deployer.DeployAsync(DeployParameters.Default);

		await mockSet.DbLiveProject.DidNotReceive().GetCodeGroupsAsync();
		await mockSet.CodeBreakingDeployer.DidNotReceive().DeployAsync();
	}

	[Fact]
	public async Task Code_breaking_scripts_run_before_migration_breaking_scripts()
	{
		MockSet mockSet = new();

		Migration migration1 = ArrangePendingMigrationBreaking(mockSet);

		List<string> calls = [];

		mockSet.CodeBreakingDeployer
			.When(x => x.DeployAsync())
			.Do(_ => calls.Add("code breaking"));

		mockSet.MigrationItemDeployer
			.When(x => x.DeployAsync(1, migration1.Items[MigrationItemType.Breaking], Arg.Any<MigrationItemSettings>()))
			.Do(_ => calls.Add("migration breaking"));

		BreakingChangesDeployer deployer = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		await deployer.DeployAsync(DeployParameters.Breaking);

		Assert.Equal(["code breaking", "migration breaking"], calls);
	}

	[Fact]
	public async Task Code_breaking_scripts_run_without_migration_breaking_scripts()
	{
		MockSet mockSet = new();

		mockSet.DbLiveDA.GetMigrationsAsync().Returns([]);
		mockSet.DbLiveProject.GetMigrationsAsync().Returns([]);

		BreakingChangesDeployer deployer = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		await deployer.DeployAsync(DeployParameters.Breaking);

		await mockSet.CodeBreakingDeployer.Received(1).DeployAsync();
	}

	[Fact]
	public async Task Guard_passes_when_every_code_file_is_applied_with_its_current_hash()
	{
		MockSet mockSet = new();

		Migration migration1 = ArrangePendingMigrationBreaking(mockSet);

		CodeItem item1 = GetCodeItem("Code/schemas/dbo.sql", "create schema dbo");
		CodeItem item2 = GetCodeItem("Code/dbo/GetUser.sql", "create or alter proc dbo.GetUser_v2\nas\nselect 1;\n");

		mockSet.DbLiveProject.GetCodeGroupsAsync().Returns([
			new CodeGroup { Path = "Code/schemas", CodeItems = [item1] },
			new CodeGroup { Path = "Code", CodeItems = [item2] }
		]);

		mockSet.DbLiveDA.GetCodeItemsAsync().Returns([
			GetCodeRow(item1, CodeItemStatus.Applied),
			// deployed from a CRLF checkout by a DbLive version that hashed the raw text: the code deployer skips it, so the guard passes.
			new CodeItemDto
			{
				RelativePath = "code/dbo/getuser.sql",
				ItemType = CodeItemType.Code,
				ContentHash = LegacyContentHash.Of("create or alter proc dbo.GetUser_v2\r\nas\r\nselect 1;\r\n"),
				Status = CodeItemStatus.Applied
			}
		]);

		BreakingChangesDeployer deployer = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		await deployer.DeployAsync(DeployParameters.Breaking);

		await mockSet.CodeBreakingDeployer.Received(1).DeployAsync();
		await mockSet.MigrationItemDeployer.Received(1)
			.DeployAsync(1, migration1.Items[MigrationItemType.Breaking], Arg.Any<MigrationItemSettings>());
	}

	[Fact]
	public async Task Guard_passes_for_a_project_without_code()
	{
		MockSet mockSet = new();

		mockSet.DbLiveProject.GetCodeGroupsAsync().Returns([]);
		mockSet.DbLiveDA.GetMigrationsAsync().Returns([]);
		mockSet.DbLiveProject.GetMigrationsAsync().Returns([]);

		BreakingChangesDeployer deployer = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		await deployer.DeployAsync(DeployParameters.Breaking);

		await mockSet.CodeBreakingDeployer.Received(1).DeployAsync();
	}

	[Fact]
	public async Task Guard_fails_and_no_breaking_script_runs_when_code_files_are_not_deployed()
	{
		MockSet mockSet = new();

		// the migration of version 1 is applied, so without the guard its breaking script would run.
		_ = ArrangePendingMigrationBreaking(mockSet);

		CodeItem applied = GetCodeItem("Code/f1.sql", "f1");
		CodeItem missing = GetCodeItem("Code/f2.sql", "f2");
		CodeItem changed = GetCodeItem("Code/f3.sql", "f3");
		CodeItem failed = GetCodeItem("Code/f4.sql", "f4");
		CodeItem removed = GetCodeItem("Code/f5.sql", "f5");
		CodeItem missing6 = GetCodeItem("Code/f6.sql", "f6");
		CodeItem missing7 = GetCodeItem("Code/f7.sql", "f7");

		mockSet.DbLiveProject.GetCodeGroupsAsync().Returns([
			new CodeGroup { Path = "Code", CodeItems = [missing7, applied, missing, changed, failed, removed, missing6] }
		]);

		// no rows for f2, f6 and f7.
		mockSet.DbLiveDA.GetCodeItemsAsync().Returns([
			GetCodeRow(applied, CodeItemStatus.Applied),
			GetCodeRow(GetCodeItem("Code/f3.sql", "f3 of the previous build"), CodeItemStatus.Applied),
			GetCodeRow(failed, CodeItemStatus.Error),
			GetCodeRow(removed, CodeItemStatus.Removed)
		]);

		BreakingChangesDeployer deployer = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		CodeNotDeployedException ex = await Assert.ThrowsAsync<CodeNotDeployedException>(
			() => deployer.DeployAsync(DeployParameters.Breaking)
		);

		Assert.Equal(6, ex.Count);
		Assert.Equal(["Code/f2.sql", "Code/f3.sql", "Code/f4.sql", "Code/f5.sql", "Code/f6.sql"], ex.RelativePaths);
		Assert.Contains("6 code file(s)", ex.Message);
		Assert.Contains("Code/f2.sql, Code/f3.sql, Code/f4.sql, Code/f5.sql, Code/f6.sql, ...", ex.Message);
		Assert.DoesNotContain("Code/f7.sql", ex.Message);
		Assert.Contains("DeployParameters.Default", ex.Message);

		await mockSet.CodeBreakingDeployer.DidNotReceive().DeployAsync();
		await mockSet.MigrationItemDeployer.DidNotReceiveWithAnyArgs()
			.DeployAsync(default, default!, default!);
	}

	[Fact]
	public async Task Guard_names_every_file_when_only_a_few_are_not_deployed()
	{
		MockSet mockSet = new();

		CodeItem item = GetCodeItem("Code/dbo/GetUser.sql", "create or alter proc dbo.GetUser_v2 as select 1;");

		mockSet.DbLiveProject.GetCodeGroupsAsync().Returns([new CodeGroup { Path = "Code", CodeItems = [item] }]);

		BreakingChangesDeployer deployer = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		CodeNotDeployedException ex = await Assert.ThrowsAsync<CodeNotDeployedException>(
			() => deployer.DeployAsync(DeployParameters.Breaking)
		);

		Assert.Equal(1, ex.Count);
		Assert.Contains("1 code file(s) are not deployed with their current content: Code/dbo/GetUser.sql. Deploy", ex.Message);
	}

	/// <summary>
	/// Version 1 has an applied migration and a breaking script that is not applied yet.
	/// </summary>
	private static Migration ArrangePendingMigrationBreaking(MockSet mockSet)
	{
		mockSet.DbLiveDA.GetMigrationsAsync().Returns([
			new MigrationItemDto
			{
				Version = 1,
				ItemType = MigrationItemType.Migration,
				Status = MigrationItemStatus.Applied,
				Name = "migration",
				RelativePath = "migrations/001.migration.sql",
				ContentHash = 111
			},
			new MigrationItemDto
			{
				Version = 1,
				ItemType = MigrationItemType.Breaking,
				Status = MigrationItemStatus.None,
				Name = "breaking",
				RelativePath = "migrations/001.breaking.sql",
				ContentHash = 1111
			}
		]);

		Migration migration1 = new()
		{
			Version = 1,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Breaking] = new()
				{
					MigrationItemType = MigrationItemType.Breaking,
					FileData = GetFileData("1.breaking.sql", "-- content 1")
				}
			}
		};

		mockSet.DbLiveProject.GetMigrationsAsync().Returns([migration1]);

		return migration1;
	}

	/// <summary>
	/// Versions 1 and 2 have applied migrations and breaking scripts that are not applied yet;
	/// the breaking script of version 2 has a settings file.
	/// </summary>
	private static (Migration Migration1, Migration Migration2) ArrangeTwoPendingMigrationBreaking(MockSet mockSet)
	{
		mockSet.DbLiveDA.GetMigrationsAsync().Returns([
			.. new long[] { 1, 2 }.SelectMany(version => new[]
			{
				new MigrationItemDto
				{
					Version = version,
					ItemType = MigrationItemType.Migration,
					Status = MigrationItemStatus.Applied,
					Name = "migration",
					RelativePath = $"migrations/00{version}.migration.sql",
					ContentHash = version * 111
				},
				new MigrationItemDto
				{
					Version = version,
					ItemType = MigrationItemType.Breaking,
					Status = MigrationItemStatus.None,
					Name = "breaking",
					RelativePath = $"migrations/00{version}.breaking.sql",
					ContentHash = version * 1111
				}
			})
		]);

		Migration migration1 = new()
		{
			Version = 1,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Breaking] = new()
				{
					MigrationItemType = MigrationItemType.Breaking,
					FileData = GetFileData("1.breaking.sql", "-- content 1")
				}
			}
		};

		Migration migration2 = new()
		{
			Version = 2,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Breaking] = new()
				{
					MigrationItemType = MigrationItemType.Breaking,
					FileData = GetFileData("2.breaking.sql", "-- content 2")
				},
				[MigrationItemType.BreakingSettings] = new()
				{
					MigrationItemType = MigrationItemType.BreakingSettings,
					FileData = GetFileData("2.breaking.settings.json", """{ "TransactionWrapLevel": "None" }""")
				}
			}
		};

		mockSet.DbLiveProject.GetMigrationsAsync().Returns([migration1, migration2]);

		return (migration1, migration2);
	}

	private static CodeItem GetCodeItem(string relativePath, string content) => new()
	{
		Name = Path.GetFileName(relativePath),
		FileData = GetFileData(relativePath, content)
	};

	private static CodeItemDto GetCodeRow(CodeItem codeItem, CodeItemStatus status) => new()
	{
		RelativePath = codeItem.FileData.RelativePath.ToLowerInvariant(),
		ItemType = CodeItemType.Code,
		ContentHash = codeItem.FileData.ContentHash,
		Status = status
	};

	private static FileData GetFileData(string relativePath, string content)
	{
		return new FileData
		{
			Content = content,
			RelativePath = relativePath
		};
	}
}