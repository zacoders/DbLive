using DbLive.xunit.SqlTest;
using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace DbLive.Tests.SqlTest;

public class SqlFactDiscovererTests
{
	[Fact]
	public void Discover_WithoutProjectDirMetadata_FindsTests_WithoutSourceInformation()
	{
		IXunitTestCase testCase = Assert.Single(Discover(nameof(SqlTestsHost.SqlAsync)));

		Assert.Equal("Tests/example.sql", testCase.DisplayName);
		Assert.Null(testCase.SourceInformation);
	}

	[Fact]
	public void Discover_WithProjectDirMetadata_GoToSourceOpensTheScriptInTheSourceProject()
	{
		IXunitTestCase testCase = Assert.Single(Discover(nameof(SqlTestsHost.SqlWithProjectDirAsync)));

		Assert.Equal("Tests/no_user_tables.sql", testCase.DisplayName);

		string expectedPath = Path.Combine(
			SqlFactDiscoverer.GetSourceScriptsPath(Assembly.Load("DemoMSSQLEmpty"))!,
			"Tests",
			"no_user_tables.sql"
		);
		Assert.Equal(Path.GetFullPath(expectedPath), testCase.SourceInformation?.FileName);
		Assert.Equal(1, testCase.SourceInformation?.LineNumber);
		Assert.True(File.Exists(testCase.SourceInformation?.FileName));
	}

	[Fact]
	public void Discover_ProjectError_Throws()
	{
		// not caught: xUnit turns an exception of a discoverer into a failing test, "Exception during discovery".
		ScriptsNotEmbeddedException ex = Assert.Throws<ScriptsNotEmbeddedException>(
			() => Discover(nameof(SqlTestsHost.SqlWithoutScriptsAsync)).ToList()
		);

		Assert.Equal("DbLive", ex.AssemblyName);
	}

	[Fact]
	public void GetSourceScriptsPath_FromProjectDirMetadata()
	{
		string? sourcePath = SqlFactDiscoverer.GetSourceScriptsPath(Assembly.Load("DemoMSSQLEmpty"));

		Assert.NotNull(sourcePath);
		Assert.EndsWith(Path.Combine("DemoMSSQLEmpty", "Scripts"), sourcePath);
		Assert.True(Directory.Exists(sourcePath));
	}

	[Fact]
	public void GetSourceScriptsPath_WithoutProjectDirMetadata_Null()
	{
		Assert.Null(SqlFactDiscoverer.GetSourceScriptsPath(typeof(SqlFactDiscovererTests).Assembly));
	}

	[Theory]
	[InlineData(null, "Scripts")]
	[InlineData("", "Scripts")]
	[InlineData("Db", "Db")]
	[InlineData(@"Db\Sql", "Db/Sql")]
	[InlineData("Db/Sql", "Db/Sql")]
	public void GetSourceScriptsPath_FromDbLiveScriptsFolderMetadata(string? scriptsFolder, string expectedFolder)
	{
		string projectDir = Path.Combine(Path.GetTempPath(), "MyDb") + Path.DirectorySeparatorChar;

		string? sourcePath = SqlFactDiscoverer.GetSourceScriptsPath(projectDir, scriptsFolder);

		Assert.Equal(Path.Combine(projectDir, expectedFolder.Replace('/', Path.DirectorySeparatorChar)), sourcePath);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void GetSourceScriptsPath_WithoutProjectDir_Null(string? projectDir)
	{
		Assert.Null(SqlFactDiscoverer.GetSourceScriptsPath(projectDir, "Db"));
	}

	[Fact]
	public void Discovered_test_case_survives_serialization()
	{
		// Visual Studio serializes the test cases between discovery and run; SqlXunitTestCase is internal.
		IXunitTestCase testCase = Assert.Single(Discover(nameof(SqlTestsHost.SqlAsync)));

		IXunitTestCase copy = SerializationHelper.Deserialize<IXunitTestCase>(SerializationHelper.Serialize(testCase));

		_ = Assert.IsType<SqlXunitTestCase>(copy);
		Assert.Equal("Tests/example.sql", copy.DisplayName);
		Assert.Equal(testCase.TestMethodArguments, copy.TestMethodArguments);
	}

	private static IEnumerable<IXunitTestCase> Discover(string methodName)
	{
		ReflectionMethodInfo method = new(typeof(SqlTestsHost).GetMethod(methodName)!);
		IAttributeInfo sqlFact = method.GetCustomAttributes(typeof(SqlFactAttribute)).Single();
		TestMethod testMethod = new(
			new TestClass(
				new TestCollection(new TestAssembly(new ReflectionAssemblyInfo(typeof(SqlTestsHost).Assembly)), null, "SQL tests"),
				new ReflectionTypeInfo(typeof(SqlTestsHost))
			),
			method
		);

		SqlFactDiscoverer discoverer = new(Substitute.For<IMessageSink>());

		return discoverer.Discover(Substitute.For<ITestFrameworkDiscoveryOptions>(), testMethod, sqlFact);
	}

	/// <summary>
	/// Abstract, so xUnit does not run its SQL tests as tests of this assembly.
	/// </summary>
	public abstract class SqlTestsHost
	{
		[SqlFact(TestFixture = typeof(EmbeddedScriptsFixture))]
		public Task SqlAsync(string testFileRelativePath) => Task.CompletedTask;

		[SqlFact(TestFixture = typeof(ProjectDirFixture))]
		public Task SqlWithProjectDirAsync(string testFileRelativePath) => Task.CompletedTask;

		[SqlFact(TestFixture = typeof(WithoutScriptsFixture))]
		public Task SqlWithoutScriptsAsync(string testFileRelativePath) => Task.CompletedTask;
	}

	public class EmbeddedScriptsFixture() : DbLiveTestFixture(new FixtureBuilder(typeof(EmbeddedScriptsFixture).Assembly), dropDatabaseOnComplete: false);

	public class ProjectDirFixture() : DbLiveTestFixture(new FixtureBuilder(Assembly.Load("DemoMSSQLEmpty")), dropDatabaseOnComplete: false);

	public class WithoutScriptsFixture() : DbLiveTestFixture(new FixtureBuilder(typeof(DbLiveBuilder).Assembly), dropDatabaseOnComplete: false);

	/// <param name="projectAssembly">This assembly, which has no ProjectDir metadata, a demo with it, or an assembly without scripts.</param>
	private sealed class FixtureBuilder(Assembly projectAssembly) : IDbLiveFixtureBuilder
	{
		public Task<DbLiveBuilder> GetBuilderAsync() => throw new NotSupportedException("Only discovery is tested.");

		public Assembly GetProjectAssembly() => projectAssembly;
	}
}
