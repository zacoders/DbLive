using System.Reflection;

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
	public void SetProject_WithProjectDirMetadata_GoToSourceUsesScriptsFolder()
	{
		using DbLiveBuilder builder = new DbLiveBuilder()
			.SetProject(Assembly.Load("DemoMSSQLEmpty"));

		string? sourcePath = builder.CreateProject().GetVisualStudioProjectPath();

		Assert.NotNull(sourcePath);
		Assert.EndsWith(Path.Combine("DemoMSSQLEmpty", "Scripts"), sourcePath);
		Assert.True(Directory.Exists(sourcePath));
	}

	[Fact]
	public void SetProject_WithoutProjectDirMetadata_NoGoToSource()
	{
		using DbLiveBuilder builder = new DbLiveBuilder()
			.SetProject(typeof(DbLiveBuilderExtensionsTests).Assembly);

		Assert.Null(builder.CreateProject().GetVisualStudioProjectPath());
	}

	[Fact]
	public void SetProject_AssemblyWithoutScripts_Throws()
	{
		using DbLiveBuilder builder = new();

		ScriptsNotEmbeddedException ex = Assert.Throws<ScriptsNotEmbeddedException>(() =>
			builder.SetProject(typeof(DbLiveBuilder).Assembly));

		Assert.Equal("DbLive", ex.AssemblyName);
		Assert.Contains("'DbLive'", ex.Message);
		Assert.Contains("""<EmbeddedResource Include="Scripts\**\*" LogicalName="DbLive/%(RecursiveDir)%(Filename)%(Extension)" WithCulture="false" />""", ex.Message);
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
		Assert.Equal(project.Path, dbLiveProject.GetVisualStudioProjectPath());
	}

	[Fact]
	public void SetScriptsFolder_RelativePath_ResolvedAgainstCurrentDirectory()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );");

		using DbLiveBuilder builder = new DbLiveBuilder()
			.SetScriptsFolder(Path.GetRelativePath(Directory.GetCurrentDirectory(), project.Path));

		Assert.Equal(project.Path, builder.CreateProject().GetVisualStudioProjectPath());
	}

	[Fact]
	public void SetScriptsFolder_MissingFolder_Throws()
	{
		using TempProject project = new();
		using DbLiveBuilder builder = new();

		_ = Assert.Throws<DirectoryNotFoundException>(() => builder.SetScriptsFolder(project.Path));
	}
}
