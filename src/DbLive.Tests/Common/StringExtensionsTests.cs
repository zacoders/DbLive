namespace DbLive.Tests.Common;

public class StringExtensionsTests
{
	[Fact]
	public void GetRelativePath_Uses_Forward_Slash()
	{
		string root = @"C:/DB";
		string filePath = Path.Combine(root, "Code", "dbo", "f.sql");

		// stored in the database, so the same on every OS.
		Assert.Equal("Code/dbo/f.sql", filePath.GetRelativePath(root));
	}
}
