namespace DbLive.Deployers;

internal static class DeployLockResource
{
	public static string Build(IDbLiveDbConnection connection, DbLiveSettings settings)
	{
		string databaseName = GetDatabaseName(connection.ConnectionString);
		string resource = $"DbLive:Deploy:{databaseName}";

		if (!string.IsNullOrWhiteSpace(settings.ProjectId))
		{
			resource += $":{settings.ProjectId.Trim()}";
		}

		return resource;
	}

	// todo: Consider using provider code, IDbLiveDbConnection can be implemented for each provider. 
	private static string GetDatabaseName(string connectionString)
	{
		foreach (string part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			int separatorIndex = part.IndexOf('=');
			if (separatorIndex <= 0)
			{
				continue;
			}

			string key = part[..separatorIndex].Trim();
			if (key.Equals("Database", StringComparison.OrdinalIgnoreCase)
				|| key.Equals("Initial Catalog", StringComparison.OrdinalIgnoreCase))
			{
				string value = part[(separatorIndex + 1)..].Trim();
				if (!string.IsNullOrWhiteSpace(value))
				{
					return value;
				}
			}
		}

		throw new DeployLockFailedException(
			"Cannot determine database name from connection string for deploy lock.");
	}
}
