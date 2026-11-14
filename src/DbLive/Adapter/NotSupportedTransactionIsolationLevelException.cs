namespace DbLive;

[ExcludeFromCodeCoverage]
public sealed class NotSupportedTransactionIsolationLevelException : Exception
{
	private TranIsolationLevel isolationLevel;

	internal NotSupportedTransactionIsolationLevelException(TranIsolationLevel isolationLevel)
		: base($"Unsupported transaction isolation level '{isolationLevel}'.")
	{
		this.isolationLevel = isolationLevel;
	}

	/// <param name="relativePath">The settings file of a migration or breaking script that sets the level; null for settings.json.</param>
	internal NotSupportedTransactionIsolationLevelException(TranIsolationLevel isolationLevel, DbProvider provider, string? relativePath)
		: base(relativePath is null
			? $"Transaction isolation level '{isolationLevel}' is not supported by {GetProviderName(provider)}."
			: $"Transaction isolation level '{isolationLevel}' in {relativePath} is not supported by {GetProviderName(provider)}.")
	{
		this.isolationLevel = isolationLevel;
	}

	private static string GetProviderName(DbProvider provider) => provider switch
	{
		DbProvider.MsSql => "SQL Server",
		DbProvider.PostgreSql => "PostgreSQL",
		_ => provider.ToString()
	};
}
