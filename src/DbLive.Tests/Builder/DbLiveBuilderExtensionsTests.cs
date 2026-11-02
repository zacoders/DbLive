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
		Assert.Contains("<DbLiveScriptsFolder>", ex.Message);
	}

	[Theory]
	[InlineData(typeof(MsSqlDA))]
	[InlineData(typeof(PostgreSqlDA))]
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
	public async Task SetScriptsFolder_SeesFilesAddedAndDeletedAfterTheBuilderWasCreated()
	{
		using TempProject project = new TempProject()
			.AddFile("Code/v1.sql", "create view dbo.v1 as select 1 as x;");

		using DbLiveBuilder builder = new DbLiveBuilder()
			.SetScriptsFolder(project.Path);

		IDbLiveProject dbLiveProject = builder.CreateProject();

		Assert.Single(Assert.Single(await dbLiveProject.GetCodeGroupsAsync()).CodeItems);

		project
			.AddFile("Code/v2.sql", "create view dbo.v2 as select 2 as x;")
			.AddFile("Code/v3.sql", "create view dbo.v3 as select 3 as x;")
			.DeleteFiles("v1.sql");

		Assert.Equal(
			["Code/v2.sql", "Code/v3.sql"],
			Assert.Single(await dbLiveProject.GetCodeGroupsAsync()).CodeItems.Select(i => i.FileData.RelativePath)
		);
	}

	[Fact]
	public void SetScriptsFolder_MissingFolder_Throws()
	{
		using TempProject project = new();
		using DbLiveBuilder builder = new();

		_ = Assert.Throws<DirectoryNotFoundException>(() => builder.SetScriptsFolder(project.Path));
	}
}
