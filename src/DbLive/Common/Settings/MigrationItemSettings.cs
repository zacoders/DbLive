namespace DbLive.Common.Settings;

/// <summary>
/// Settings file of one migration item: <c>&lt;version&gt;.migration.settings.json</c>,
/// <c>&lt;version&gt;.breaking.settings.json</c> or <c>&lt;version&gt;.undo.settings.json</c>.
/// A missing key falls back to the project settings.
/// </summary>
public record MigrationItemSettings
{
	/// <summary>
	/// <see cref="TransactionWrapLevel.Migration"/> or <see cref="TransactionWrapLevel.None"/>,
	/// <see cref="TransactionWrapLevel.Deployment"/> is a project setting only.
	/// </summary>
	public TransactionWrapLevel? TransactionWrapLevel { get; init; }

	public TranIsolationLevel? TransactionIsolationLevel { get; init; }

	/// <summary>
	/// Transaction and command timeout of the item. Defaults to the project <see cref="DbLiveSettings.MigrationTimeout"/>.
	/// </summary>
	public TimeSpan? Timeout { get; init; }
}
