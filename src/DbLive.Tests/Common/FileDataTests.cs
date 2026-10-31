namespace DbLive.Tests.Common;

public class FileDataTests
{
	private const string Lf = "create table t1 ( id int );\ngo\n";
	private const string CrLf = "create table t1 ( id int );\r\ngo\r\n";

	[Fact]
	public void ContentHash_Does_Not_Depend_On_Line_Endings()
	{
		Assert.Equal(NewFileData(Lf).ContentHash, NewFileData(CrLf).ContentHash);
	}

	[Fact]
	public void Content_Keeps_Line_Endings()
	{
		Assert.Equal(CrLf, NewFileData(CrLf).Content);
	}

	[Fact]
	public void ContentHash_Changes_With_Content()
	{
		Assert.NotEqual(NewFileData(Lf).ContentHash, NewFileData(Lf.Replace("t1", "t2")).ContentHash);
	}

	[Theory]
	[InlineData("a\rb")]
	[InlineData("a\u2028b")]
	[InlineData("a\u0085b")]
	public void Only_CrLf_Is_Normalized(string content)
	{
		Assert.NotEqual(NewFileData("a\nb").ContentHash, NewFileData(content).ContentHash);
	}

	[Theory]
	[InlineData(CrLf, Lf)] // applied from a Windows checkout, deployed from a Linux checkout
	[InlineData(CrLf, CrLf)]
	[InlineData(Lf, CrLf)]
	[InlineData(Lf, Lf)]
	public void MatchesHash_Accepts_Legacy_Hash_With_Any_Line_Endings(string storedContent, string fileContent)
	{
		Assert.True(NewFileData(fileContent).MatchesHash(LegacyContentHash.Of(storedContent)));
	}

	[Fact]
	public void MatchesHash_Accepts_Legacy_Hash_Of_Mixed_Line_Endings()
	{
		string mixed = "select 1;\r\nselect 2;\n";

		Assert.True(NewFileData(mixed).MatchesHash(LegacyContentHash.Of(mixed)));
	}

	[Fact]
	public void MatchesHash_Accepts_ContentHash()
	{
		FileData fileData = NewFileData(CrLf);

		Assert.True(fileData.MatchesHash(fileData.ContentHash));
	}

	[Fact]
	public void MatchesHash_Rejects_Other_Content()
	{
		string other = CrLf.Replace("t1", "t2");

		Assert.False(NewFileData(CrLf).MatchesHash(LegacyContentHash.Of(other)));
		Assert.False(NewFileData(CrLf).MatchesHash(LegacyContentHash.Of(other.Replace("\r\n", "\n"))));
		Assert.False(NewFileData(CrLf).MatchesHash(NewFileData(other).ContentHash));
	}

	private static FileData NewFileData(string content)
	{
		return new FileData
		{
			Content = content,
			RelativePath = "Migrations/001.migration.sql",
			FilePath = "c:/db/Migrations/001.migration.sql"
		};
	}
}
