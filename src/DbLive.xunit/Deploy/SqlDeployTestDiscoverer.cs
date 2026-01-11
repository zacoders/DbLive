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
