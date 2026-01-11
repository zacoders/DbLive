using DbLive.Common;
using DbLive.Project;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace DbLive.xunit.SqlTest;

public class SqlFactDiscoverer(IMessageSink diagnosticMessageSink) : IXunitTestCaseDiscoverer
{
	public IMessageSink DiagnosticMessageSink { get; } = diagnosticMessageSink ?? throw new ArgumentNullException(nameof(diagnosticMessageSink));

	public IEnumerable<IXunitTestCase> Discover(
		ITestFrameworkDiscoveryOptions discoveryOptions,
		ITestMethod testMethod,
		IAttributeInfo factAttribute
	)
	{
		var attr = (ReflectionAttributeInfo)factAttribute;

		Type fixtureType = attr.GetNamedArgument<Type>(
			nameof(SqlFactAttribute.TestFixture)
		);

		//if (!typeof(DbLiveTestingFixture).IsAssignableFrom(fixtureType))
		//{
		//	throw new WrongDbLiveTestingFixtureTypeException(
		//		$"The type specified in {nameof(SqlFactAttribute)}.{nameof(SqlFactAttribute.DbLiveTestingFixture)} must implement {nameof(DbLiveTestingFixture)} type."
		//	);
		//}
		
		if (!typeof(DbLiveTestFixtureBase).IsAssignableFrom(fixtureType))
		{
			string attributeName = nameof(SqlFactAttribute).Replace("Attribute", "");
			string paramName = nameof(SqlFactAttribute.TestFixture);
			string spaces = new(' ', paramName.Length + attributeName.Length);

			string errorMessage = $$"""
				Invalid type specified for {{paramName}} in {{attributeName}} attribute.

				The type must inherit from: {{nameof(DbLiveTestFixtureBase)}}.
				Actual type: {{fixtureType.FullName}}.

				Example:

				class MyTestFixture : {{nameof(DbLiveTestFixtureBase)}}
				{
					...
				}
				{{spaces}}            🡳🡳🡳
				[{{attributeName}}({{paramName}} = typeof(MyTestFixture))]
				public async Task Sql(string testRelativePath)
				{
					...
				}
				""";
			yield return new ExecutionErrorTestCase(
				DiagnosticMessageSink,
				discoveryOptions.MethodDisplayOrDefault(),
				discoveryOptions.MethodDisplayOptionsOrDefault(),
				testMethod,
				errorMessage
			);
			yield break;
		}

		var fixture = (DbLiveTestFixtureBase)Activator.CreateInstance(fixtureType)!;

		string projectPath = fixture.GetProjectPath();
		
		IDbLiveProject project = new DbLiveBuilder()
			.SetProjectPath(projectPath)
			.CreateProject();


#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits

		string root = project
			.GetVisualStudioProjectPathAsync()
			.GetAwaiter()
			.GetResult();

		IReadOnlyCollection<TestItem> tests = project
			.GetTestsAsync()
			.GetAwaiter()
			.GetResult();

#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits

		foreach (Project.TestItem testItem in tests)
		{
			yield return new SqlXunitTestCase(
				DiagnosticMessageSink,
				testMethod,
				Path.Combine(root, testItem.FileData.RelativePath),
				testItem.FileData.RelativePath
			);
		}
	}
}
