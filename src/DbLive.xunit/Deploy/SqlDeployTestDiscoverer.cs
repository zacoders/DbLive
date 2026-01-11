using DbLive.Common;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace DbLive.xunit.Deploy;

public sealed class SqlDeployTestDiscoverer(IMessageSink diagnosticMessageSink)
	: IXunitTestCaseDiscoverer
{
	public IMessageSink DiagnosticMessageSink { get; } = diagnosticMessageSink ?? throw new ArgumentNullException(nameof(diagnosticMessageSink));

	public IEnumerable<IXunitTestCase> Discover(
		ITestFrameworkDiscoveryOptions discoveryOptions,
		ITestMethod testMethod,
		IAttributeInfo factAttribute)
	{
		// test
		//yield return new SqlDeployXunitTestCase(
		//				DiagnosticMessageSink,
		//				testMethod,
		//				false, 
		//				UndoTestMode.None
		//			);

		//var attr = (ReflectionAttributeInfo)factAttribute;

		//Type fixtureType = attr.GetNamedArgument<Type>(
		//	nameof(SqlDeployTestAttribute.TestFixture)
		//);

		//if (!typeof(DeployFixtureBase).IsAssignableFrom(fixtureType))
		//{
		//	yield return new ExecutionErrorTestCase(
		//		DiagnosticMessageSink,
		//		discoveryOptions.MethodDisplayOrDefault(),
		//		discoveryOptions.MethodDisplayOptionsOrDefault(),
		//		testMethod,
		//		$"SqlDeployTestAttribute.DbLiveTestingFixture must inherit from {nameof(DeployFixtureBase)}. Actual: {fixtureType.FullName}"
		//	);
		//	yield break;
		//}

		UndoTestMode[] undoModes =
		[
			UndoTestMode.None,
			UndoTestMode.MigrationUndoMigration,
			UndoTestMode.MigrationBreakingUndoMigration
		];

		foreach (UndoTestMode undoTestMode in undoModes)
		{
			foreach (bool breaking in new[] { false, true })
			{
				//var ctx = new SqlDeployTestContext
				//{
				//	DeployBreaking = breaking,
				//	UndoTestMode = undoTestMode//,
				//	//FixtureType = fixtureType
				//};

				yield return new SqlDeployXunitTestCase(
					DiagnosticMessageSink,
					testMethod,
					breaking,
					undoTestMode
				);
			}
		}
	}
}
