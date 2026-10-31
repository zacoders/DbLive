using DbLive.xunit;
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
		ReflectionMethodInfo method = new(typeof(SqlTestsHost).GetMethod(nameof(SqlTestsHost.SqlAsync))!);
		IAttributeInfo sqlFact = method.GetCustomAttributes(typeof(SqlFactAttribute)).Single();
		TestMethod testMethod = new(
			new TestClass(
				new TestCollection(new TestAssembly(new ReflectionAssemblyInfo(typeof(SqlTestsHost).Assembly)), null, "SQL tests"),
				new ReflectionTypeInfo(typeof(SqlTestsHost))
			),
			method
		);

		SqlFactDiscoverer discoverer = new(Substitute.For<IMessageSink>());

		IXunitTestCase testCase = Assert.Single(
			discoverer.Discover(Substitute.For<ITestFrameworkDiscoveryOptions>(), testMethod, sqlFact));

		Assert.Equal("Tests/example.sql", testCase.DisplayName);
		Assert.Null(testCase.SourceInformation);
	}

	/// <summary>
	/// Abstract, so xUnit does not run its SQL tests as tests of this assembly.
	/// </summary>
	public abstract class SqlTestsHost
	{
		[SqlFact(TestFixture = typeof(EmbeddedScriptsFixture))]
		public Task SqlAsync(string testFileRelativePath) => Task.CompletedTask;
	}

	public class EmbeddedScriptsFixture() : DbLiveTestFixture(new EmbeddedScriptsFixtureBuilder(), dropDatabaseOnComplete: false);

	/// <summary>
	/// The scripts embedded in this assembly, which has no ProjectDir metadata.
	/// </summary>
	private sealed class EmbeddedScriptsFixtureBuilder : IDbLiveFixtureBuilder
	{
		public Task<DbLiveBuilder> GetBuilderAsync() => throw new NotSupportedException("Only discovery is tested.");

		public Assembly GetProjectAssembly() => typeof(EmbeddedScriptsFixtureBuilder).Assembly;
	}
}
