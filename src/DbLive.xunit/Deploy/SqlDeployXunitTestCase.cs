using DbLive.Common;
using System.ComponentModel;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace DbLive.xunit.Deploy;

public sealed class SqlDeployXunitTestCase : XunitTestCase
{
	//private SqlDeployTestContext Context { get; set; }

	private bool DeployBreaking { get; set; }
	private UndoTestMode UndoTestMode { get; set; }

	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete("Called by the de-serializer", true)]
#pragma warning disable CS8618
	public SqlDeployXunitTestCase() { }
#pragma warning restore CS8618

	public SqlDeployXunitTestCase(
		IMessageSink sink,
		ITestMethod testMethod,
		bool deployBreaking, 
		UndoTestMode undoTestMode
	)
		: base(
			sink,
			TestMethodDisplay.Method,
			TestMethodDisplayOptions.None,
			testMethod
		)
	{
		DeployBreaking = deployBreaking;
		UndoTestMode = undoTestMode;
		TestMethodArguments = [deployBreaking, undoTestMode];
	}


	protected override string GetDisplayName(IAttributeInfo factAttribute, string displayName)
	{
		return $"{TestMethod.TestClass.Class.Name}.{TestMethod.Method.Name} " +
			   $"(breaking={DeployBreaking}, undo={UndoTestMode})";
	}
}
