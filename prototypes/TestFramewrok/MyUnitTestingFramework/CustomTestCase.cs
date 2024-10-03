using Xunit.Abstractions;
using Xunit.Sdk;

namespace MyUnitTestingFramework;

public class CustomTestCase : XunitTestCase
{
	[Obsolete("Called by the de-serializer", true)]
	public CustomTestCase() { }

	public CustomTestCase(IMessageSink sink, ITestMethod testMethod, string testFilePath)
		: base(
			sink, 
			TestMethodDisplay.Method, 
			TestMethodDisplayOptions.None, 
			testMethod
			// new TestMethod(testMethod.TestClass, new MyMethodInfo(testMethod.Method, "Test123"))
		)
	{
		//DisplayName = Path.GetFileName(testFilePath);
		//DisplayName = testFilePath;
		
		SourceInformation = new SourceInformation
		{
			FileName = testFilePath,
			LineNumber = 1
		};

		//TestMethodArguments = [Path.GetFileName(testFilePath), testFilePath];
		TestMethodArguments = [testFilePath];
		//testMethod.Method = new TestMethod()
		//DisplayName = "MyName1";
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

	//public override string DisplayName => $"{_folderName}.{base.DisplayName}";
	protected override string GetDisplayName(IAttributeInfo factAttribute, string displayName)
		=> TestMethodArguments[0].ToString();
}
