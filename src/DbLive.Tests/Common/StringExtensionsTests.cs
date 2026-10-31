namespace DbLive.Tests.Common;

public class StringExtensionsTests
{
	[Theory]
	[InlineData(@"db\Code")]
	[InlineData("db/Code")]
	[InlineData("db/Code/")]
	[InlineData(@"\db\Code\")]
	[InlineData("/db/Code")]
	[InlineData(@"db/Code\")]
	public void CombineWithRelative_Normalizes_Separators(string relativePath)
	{
		string root = @"C:/DB";

		string actual = root.CombineWithRelative(relativePath);

		// enumerated files always use the OS path separator.
		Assert.Equal(root.CombineWith("db").CombineWith("Code"), actual);
	}

	[Fact]
	public void GetRelativePath_Uses_Forward_Slash()
	{
		string root = @"C:/DB";
		string filePath = root.CombineWith("Code").CombineWith("dbo").CombineWith("f.sql");

		// stored in the database, so the same on every OS.
		Assert.Equal("Code/dbo/f.sql", filePath.GetRelativePath(root));
	}

	[Fact]
	public void CombineWithRelative_TopLevelFolder()
	{
		string root = @"C:/DB";

		Assert.Equal(root.CombineWith("Code"), root.CombineWithRelative("Code"));
	}
}
