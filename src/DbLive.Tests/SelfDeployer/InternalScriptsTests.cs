using DbLive.MSSQL;
using DbLive.PostgreSQL;
using System.Reflection;

namespace DbLive.Tests.SelfDeployer;

/// <summary>
/// DbLive's own scripts are embedded in the provider assemblies and compared with the database only by version.
/// </summary>
public class InternalScriptsTests
{
	[Theory]
	[InlineData(typeof(MsSqlFixtureBuilder), "DbLive.MSSQL")]
	[InlineData(typeof(PostgreSqlFixtureBuilder), "DbLive.PostgreSQL")]
	public void Provider_embeds_all_its_SQL_files(Type providerType, string projectFolder)
	{
		string sqlFolder = Path.Combine(GetSrcDir(), projectFolder, "SQL");

		string[] expected = [.. Directory.EnumerateFiles(sqlFolder, "*.sql", SearchOption.AllDirectories)
			.Select(f => f.GetRelativePath(sqlFolder))
			.Order(StringComparer.Ordinal)];

		InternalScriptSource scriptSource = new(providerType.Assembly);

		Assert.NotEmpty(expected);
		Assert.Equal(expected, scriptSource.GetFiles().Order(StringComparer.Ordinal));
		Assert.All(
			providerType.Assembly.GetManifestResourceNames().Where(name => name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)),
			name => Assert.StartsWith(InternalScriptSource.Prefix, name)
		);
	}

	[Theory]
	[InlineData(typeof(MsSqlFixtureBuilder))]
	[InlineData(typeof(PostgreSqlFixtureBuilder))]
	public async Task Provider_versions_are_1_to_N_without_gaps(Type providerType)
	{
		InternalDbLiveProject project = new(new InternalScriptSource(providerType.Assembly));

		IReadOnlyList<InternalMigration> migrations = await project.GetMigrationsAsync();

		Assert.Equal(Enumerable.Range(1, migrations.Count).Select(v => (long)v), migrations.Select(m => m.Version));
		Assert.All(migrations, m => Assert.False(string.IsNullOrWhiteSpace(m.FileData.Content)));
	}

	private static string GetSrcDir() =>
		typeof(InternalScriptsTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "SrcDir").Value!;
}
