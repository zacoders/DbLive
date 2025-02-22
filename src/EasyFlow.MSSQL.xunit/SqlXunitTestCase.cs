using Xunit.Abstractions;
using Xunit.Sdk;

namespace EasyFlow.xunit;

public class SqlXunitTestCase : XunitTestCase
{
	private string RelativeFilePath { get; set; }
	private string TestFilePathVS { get; set; }


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
		TestFilePathVS = testFilePathVS;
		RelativeFilePath = relativeFilePath;

		TestMethodArguments = [relativeFilePath];
	}

	public override void Deserialize(IXunitSerializationInfo info)
	{
		base.Deserialize(info);
		RelativeFilePath = info.GetValue<string>("MY_RelativeFilePath");
		TestFilePathVS = info.GetValue<string>("MY_TestFilePathVS");
	}

	public override void Serialize(IXunitSerializationInfo info)
	{
		base.Serialize(info);
		info.AddValue("MY_RelativeFilePath", RelativeFilePath, typeof(string));
		info.AddValue("MY_TestFilePathVS", TestFilePathVS, typeof(string));
	}

	protected override string GetDisplayName(IAttributeInfo factAttribute, string displayName)
	{
		PopulateSourceInformation(TestFilePathVS, 1);

		if (!string.IsNullOrWhiteSpace(RelativeFilePath)) return RelativeFilePath;

		if (TestMethodArguments.Length > 0 && TestMethodArguments[0] is not null)
		{
			object pathToTestFile = TestMethodArguments[0];
			return pathToTestFile.ToString();
		}

		return base.GetDisplayName(factAttribute, displayName);
	}

	private void PopulateSourceInformation(string fileName, int lineNumber)
	{
		if (SourceInformation == null)
		{
			SourceInformation = new SourceInformation
			{
				FileName = fileName,
				LineNumber = lineNumber
			};
		}
		else
		{
			SourceInformation.FileName = fileName;
			SourceInformation.LineNumber = lineNumber;
		}
	}
}
