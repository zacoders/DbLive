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

		if (!typeof(DbLiveTestFixture).IsAssignableFrom(fixtureType))
		{
			string attributeName = nameof(SqlFactAttribute).Replace("Attribute", "");
			string paramName = nameof(SqlFactAttribute.TestFixture);
			string spaces = new(' ', paramName.Length + attributeName.Length);

			string errorMessage = $$"""
				Invalid type specified for {{paramName}} in {{attributeName}} attribute.

				The type must inherit from: {{nameof(DbLiveTestFixture)}}.
				Actual type: {{fixtureType.FullName}}.

				Example:

				class MyTestFixture : {{nameof(DbLiveTestFixture)}}
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

		var fixture = (DbLiveTestFixture)Activator.CreateInstance(fixtureType)!;


#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits

		fixture.InitializeAsync()
			.GetAwaiter()
			.GetResult();

		if (fixture.Project is null)
		{
			throw new ArgumentNullException(nameof(fixture.Project));
		}

		string root = fixture.Project
			.GetVisualStudioProjectPathAsync()
			.GetAwaiter()
			.GetResult();

		IReadOnlyCollection<TestItem> tests = fixture.Project!
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
