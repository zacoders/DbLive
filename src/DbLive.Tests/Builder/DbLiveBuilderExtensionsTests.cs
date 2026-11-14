using DbLive.MSSQL;
using DbLive.PostgreSQL;

namespace DbLive.Tests.Builder;

public class DbLiveBuilderExtensionsTests
{
	[Fact]
	public async Task SetProject_ReadsScriptsEmbeddedInAssembly()
	{
		using DbLiveBuilder builder = new DbLiveBuilder()
			.SetProject(typeof(DbLiveBuilderExtensionsTests).Assembly);

		IReadOnlyCollection<TestItem> tests = await builder.CreateProject().GetTestsAsync();

		TestItem test = Assert.Single(tests);
		Assert.Equal("Tests/example.sql", test.FileData.RelativePath);
		Assert.Equal("select 1 as x;", test.FileData.Content);
	}

	[Fact]
	public void SetProject_AssemblyWithoutScripts_Throws()
	{
		using DbLiveBuilder builder = new();

		ScriptsNotEmbeddedException ex = Assert.Throws<ScriptsNotEmbeddedException>(() =>
			builder.SetProject(typeof(DbLiveBuilder).Assembly));

		Assert.Equal("DbLive", ex.AssemblyName);
		Assert.Contains("'DbLive'", ex.Message);
		Assert.Contains("""<PackageReference Include="DbLive" />""", ex.Message);
		Assert.Contains("<DbLiveScriptsFolder>Scripts</DbLiveScriptsFolder>", ex.Message);
	}

	[Theory]
	[InlineData(typeof(MsSqlFixtureBuilder))]
	[InlineData(typeof(PostgreSqlFixtureBuilder))]
	public void SetProject_ProviderAssembly_Throws(Type providerType)
	{
		using DbLiveBuilder builder = new();

		// DbLive's own scripts in the provider assembly are not the project's scripts.
		ScriptsNotEmbeddedException ex = Assert.Throws<ScriptsNotEmbeddedException>(() =>
			builder.SetProject(providerType.Assembly));

		Assert.Equal(providerType.Assembly.GetName().Name, ex.AssemblyName);
	}

	[Fact]
	public async Task SetScriptsFolder_ReadsScriptsFromFolder()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );");

		using DbLiveBuilder builder = new DbLiveBuilder()
			.SetScriptsFolder(project.Path);

		IDbLiveProject dbLiveProject = builder.CreateProject();

		Migration migration = Assert.Single(await dbLiveProject.GetMigrationsAsync());
		Assert.Equal("Migrations/001.migration.sql", migration.Items[MigrationItemType.Migration].FileData.RelativePath);
		Assert.Equal("create table dbo.t1 ( id int not null );", migration.Items[MigrationItemType.Migration].FileData.Content);
	}

	[Fact]
	public async Task SetScriptsFolder_RelativePath_ResolvedAgainstCurrentDirectory()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );");

		using DbLiveBuilder builder = new DbLiveBuilder()
			.SetScriptsFolder(Path.GetRelativePath(Directory.GetCurrentDirectory(), project.Path));

		Assert.Single(await builder.CreateProject().GetMigrationsAsync());
	}

	[Fact]
	public async Task SetScriptsFolder_ReadsTheProjectOncePerBuilder()
	{
		using TempProject project = new TempProject()
			.AddFile("Code/v1.sql", "create view dbo.v1 as select 1 as x;");

		using DbLiveBuilder builder = new DbLiveBuilder()
			.SetScriptsFolder(project.Path);

		Assert.Single(Assert.Single(await builder.CreateProject().GetCodeGroupsAsync()).CodeItems);

		project
			.AddFile("Code/v1.sql", "create view dbo.v1 as select 10 as x;")
			.AddFile("Code/v2.sql", "create view dbo.v2 as select 2 as x;");

		// the same builder: the files as they were at the first read.
		CodeItem item = Assert.Single(Assert.Single(await builder.CreateProject().GetCodeGroupsAsync()).CodeItems);
		Assert.Equal("create view dbo.v1 as select 1 as x;", item.FileData.Content);

		// a new builder for the new build.
		using DbLiveBuilder newBuilder = new DbLiveBuilder()
			.SetScriptsFolder(project.Path);

		Assert.Equal(
			["create view dbo.v1 as select 10 as x;", "create view dbo.v2 as select 2 as x;"],
			Assert.Single(await newBuilder.CreateProject().GetCodeGroupsAsync()).CodeItems.Select(i => i.FileData.Content)
		);
	}

	[Fact]
	public void SetScriptsFolder_MissingFolder_Throws()
	{
		using TempProject project = new();
		using DbLiveBuilder builder = new();

		_ = Assert.Throws<DirectoryNotFoundException>(() => builder.SetScriptsFolder(project.Path));
	}

	[Fact]
	public async Task SetScriptSource_ReadsScriptsFromTheSource()
	{
		IScriptSource scriptSource = new InMemoryScriptSource()
			.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );");

		using DbLiveBuilder builder = new DbLiveBuilder()
			.SetScriptSource(scriptSource);

		Migration migration = Assert.Single(await builder.CreateProject().GetMigrationsAsync());
		Assert.Equal("create table dbo.t1 ( id int not null );", migration.Items[MigrationItemType.Migration].FileData.Content);
	}

	[Fact]
	public void SetScriptSource_Null_Throws()
	{
		using DbLiveBuilder builder = new();

		_ = Assert.Throws<ArgumentNullException>(() => builder.SetScriptSource(null!));
	}

	[Fact]
	public void LogTo_LogsToTheLogger()
	{
		Serilog.ILogger logger = Substitute.For<Serilog.ILogger>();

		using DbLiveBuilder builder = new DbLiveBuilder()
			.LogTo(logger);

		Assert.Same(logger, builder.GetRequiredService<Serilog.ILogger>());
	}

	[Fact]
	public void LogTo_Null_Throws()
	{
		using DbLiveBuilder builder = new();

		_ = Assert.Throws<ArgumentNullException>(() => builder.LogTo(null!));
	}
}
