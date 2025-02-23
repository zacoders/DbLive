using System.ComponentModel;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace EasyFlow.xunit;

public class SqlXunitTestCase : XunitTestCase
{
	private string TestFileFullPath { get; set; }

	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete("Called by the de-serializer", true)]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. 
	public SqlXunitTestCase() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor.

	/// <summary>
	/// Initialize Test Case
	/// </summary>
	/// <param name="sink"></param>
	/// <param name="testMethod"></param>
	/// <param name="testFileFullPath">Test file path in Visual Studio IDE. It should point directly to the file in Visual Studio Project.</param>
	/// <param name="testFileRelativePath">Relative path to the testing file.</param>
	public SqlXunitTestCase(IMessageSink sink, ITestMethod testMethod, string testFileFullPath, string testFileRelativePath)
		: base(
			sink,
			TestMethodDisplay.Method,
			TestMethodDisplayOptions.None,
			testMethod
		)
	{		
		TestFileFullPath = testFileFullPath;

		TestMethodArguments = [testFileRelativePath];
	}

	protected override string GetDisplayName(IAttributeInfo factAttribute, string displayName)
	{
		PopulateSourceInformation(TestFileFullPath, 1);

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
