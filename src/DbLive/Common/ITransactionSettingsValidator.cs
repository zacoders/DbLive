namespace DbLive.Common;

internal interface ITransactionSettingsValidator
{
	Task ValidateAsync(DbLiveSettings settings);

	/// <summary>
	/// Checks the <c>TransactionIsolationLevel</c> of the settings file of every migration and breaking script against the
	/// provider, so a level the provider does not support stops the deployment before the first migration.
	/// </summary>
	/// <exception cref="NotSupportedTransactionIsolationLevelException"/>
	void ValidateMigrations(IReadOnlyList<Migration> migrations);
}
