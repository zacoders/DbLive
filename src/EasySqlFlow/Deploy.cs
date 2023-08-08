
public class DeploySQL
{
	public void Deploy(string path)
	{
		string migrationsPath = Path.Combine(path, "Migrations");

		var migrations = GetMigrations(migrationsPath);

		//TODO: check each migration folder.
	}

	private static HashSet<Migration> GetMigrations(string path)
	{
		HashSet<Migration> migrations = new();
		foreach (string folderPath in Directory.EnumerateDirectories(path, "*.*", SearchOption.AllDirectories))
		{
			Uri folderUri = new Uri(folderPath);
			string folderName = folderUri.GetFolder();

			if (folderName == "_Old") continue;

			var migration = ParsePath(folderUri);

			if (migrations.Contains(migration))
			{
				throw new MigrationExistsException(migration);
			}

			migrations.Add(migration);
		}
		return migrations;
	}

	private static Migration ParsePath(Uri folderUri)
	{		
		string folderName = folderUri.GetFolder();
		var splitFolder = folderName.Split(".");
		
		string migrationVersionStr = splitFolder[0];

		if (!int.TryParse(migrationVersionStr, out var version))
		{
			throw new MigrationVersionParseException(folderName, migrationVersionStr);
		}

		return new Migration
		{
			Version = version,
			Name = splitFolder[1],
			PathUri = folderUri
		};
	}
}