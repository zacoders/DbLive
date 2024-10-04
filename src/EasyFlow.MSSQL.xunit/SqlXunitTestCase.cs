using Xunit.Abstractions;
using Xunit.Sdk;

namespace EasyFlow.MSSQL.xunit;

public class SqlXunitTestCase : XunitTestCase
{
	[Obsolete("Called by the de-serializer", true)]
	public SqlXunitTestCase() { }

	/// <summary>
	/// Initialize Test Case
	/// </summary>
	/// <param name="sink"></param>
	/// <param name="testMethod"></param>
	/// <param name="testFilePathVS">Test file path in Visual Studio IDE. It should point directly to the file in Visual Studio Project.</param>
	/// <param name="relativeFilePath">Relative path to the testing file.</param>
	public SqlXunitTestCase(IMessageSink sink, ITestMethod testMethod, string testFilePathVS, string relativeFilePath)
		: base(
			sink,
			TestMethodDisplay.Method,
			TestMethodDisplayOptions.None,
			testMethod
		)
	{
		SourceInformation = new SourceInformation
		{
			FileName = testFilePathVS,
			LineNumber = 1
		};

		TestMethodArguments = [relativeFilePath];
	}

	public override void Deserialize(IXunitSerializationInfo info)
	{
		base.Deserialize(info);
		//_folderName = info.GetValue<string>("FolderName");
	}

	public override void Serialize(IXunitSerializationInfo info)
	{
		base.Serialize(info);
		//info.AddValue("FolderName", _folderName);
	}

	protected override string GetDisplayName(IAttributeInfo factAttribute, string displayName)
	{
		string attrDisplayName = factAttribute.GetNamedArgument<string>("DisplayName");
		if (TestMethodArguments.Length > 0 && TestMethodArguments[0] is not null)
		{
			object pathToTestFile = TestMethodArguments[0];
			if (string.IsNullOrWhiteSpace(attrDisplayName))
			{
				return $"{pathToTestFile}";
			}
			else
			{
				return $"{attrDisplayName}: {pathToTestFile}";
			}
		}

		return base.GetDisplayName(factAttribute, displayName);
	}
}
