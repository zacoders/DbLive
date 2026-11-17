namespace DbLive.Common;

internal class TransactionSettingsValidator(
		IDbLiveDA _da,
		ILogger _logger
	) : ITransactionSettingsValidator
{
	private readonly ILogger _logger = _logger.ForContext(typeof(TransactionSettingsValidator));

	public Task ValidateAsync(DbLiveSettings settings)
	{
		TranIsolationLevelMapper.ValidateForProvider(settings.TransactionIsolationLevel, _da.Provider);
		TranIsolationLevelMapper.ValidateForProvider(settings.TestsTransactionIsolationLevel, _da.Provider);

		if (settings.TransactionWrapLevel != TransactionWrapLevel.None)
		{
			_ = TranIsolationLevelMapper.ToSystemTransaction(settings.TransactionIsolationLevel);
			_ = TranIsolationLevelMapper.ToSql(settings.TransactionIsolationLevel, _da.Provider);
		}

		if (settings.TransactionIsolationLevel == TranIsolationLevel.Snapshot
			&& _da.Provider == DbProvider.MsSql)
		{
			_logger.Warning(
				"Snapshot isolation requires ALLOW_SNAPSHOT_ISOLATION ON on the database."
			);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public void ValidateMigrations(IReadOnlyList<Migration> migrations)
	{
		foreach (Migration migration in migrations)
		{
			foreach (MigrationItem settingsItem in migration.Items.Values.Where(i => MigrationItemTypeMapper.IsSettings(i.MigrationItemType)))
			{
				MigrationItemSettings settings = SettingsTools.GetMigrationItemSettings(settingsItem.FileData.Content, settingsItem.FileData.RelativePath);

				if (settings.TransactionIsolationLevel is TranIsolationLevel isolationLevel)
				{
					TranIsolationLevelMapper.ValidateForProvider(isolationLevel, _da.Provider, settingsItem.FileData.RelativePath);
				}
			}
		}
	}
}
