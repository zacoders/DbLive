using System.Text;

namespace DbLive.Tests.Common;

public class EmbeddedFileSystemTests
{
	private const string Root = "MyDb.dll";

	[Fact]
	public void EnumerateFiles_ReturnsPathsUnderRoot_WithOsSeparator()
	{
		// %(RecursiveDir) uses '\' when the project is built on Windows and '/' on Linux.
		EmbeddedFileSystem fileSystem = Create(
			"DbLive/Migrations\\001.migration.sql",
			"DbLive/Code/dbo/v1.sql"
		);

		Assert.Equal(
			[P("Migrations", "001.migration.sql"), P("Code", "dbo", "v1.sql")],
			fileSystem.EnumerateFiles(Root, "*.sql", subfolders: true)
		);
	}

	[Fact]
	public void EnumerateFiles_IgnoresResourcesWithoutPrefix()
	{
		EmbeddedFileSystem fileSystem = Create(
			"DbLive/Code/v1.sql",
			"MyDb.Properties.Resources.resources",
			"Other/Code/v2.sql"
		);

		Assert.Equal([P("Code", "v1.sql")], fileSystem.EnumerateFiles(Root, "*.sql", subfolders: true));
	}

	[Fact]
	public void IsEmpty_WithoutPrefixedResources()
	{
		Assert.True(Create("MyDb.Properties.Resources.resources").IsEmpty);
		Assert.False(Create("DbLive/settings.json").IsEmpty);
	}

	[Fact]
	public void EnumerateFiles_MatchesPatterns_AndExcludePatterns()
	{
		EmbeddedFileSystem fileSystem = Create(
			"DbLive/Code/a.sql",
			"DbLive/Code/a.test.sql",
			"DbLive/Code/setup.test.sql",
			"DbLive/Code/notes.txt",
			"DbLive/Code/a.sqlx"
		);

		Assert.Equal(
			[P("Code", "a.sql")],
			fileSystem.EnumerateFiles(P("Code"), ["*.sql"], ["*.test.sql", "setup.test.sql"], subfolders: true)
		);

		Assert.Equal(
			[P("Code", "a.test.sql"), P("Code", "setup.test.sql")],
			fileSystem.EnumerateFiles(P("Code"), "*.test.sql", subfolders: true)
		);

		Assert.Equal(
			[P("Code", "a.test.sql")],
			fileSystem.EnumerateFiles(P("Code"), "*.test.sql", "setup.test.sql", subfolders: true)
		);
	}

	[Fact]
	public void EnumerateFiles_PatternByPattern()
	{
		EmbeddedFileSystem fileSystem = Create(
			"DbLive/Migrations/001.migration.settings.json",
			"DbLive/Migrations/001.migration.sql"
		);

		// like FileSystem, which enumerates the folder once per pattern.
		Assert.Equal(
			[P("Migrations", "001.migration.sql"), P("Migrations", "001.migration.settings.json")],
			fileSystem.EnumerateFiles(P("Migrations"), ["*.sql", "*.json"], subfolders: true)
		);
	}

	[Fact]
	public void EnumerateFiles_TopDirectoryOnly_WithoutSubfolders()
	{
		EmbeddedFileSystem fileSystem = Create(
			"DbLive/Tests/t1.sql",
			"DbLive/Tests/sub/t2.sql"
		);

		Assert.Equal([P("Tests", "t1.sql")], fileSystem.EnumerateFiles(P("Tests"), "*.sql"));
	}

	[Fact]
	public void EnumerateFiles_InTheOrderOfDirectoryEnumerateFilesOnWindows()
	{
		EmbeddedFileSystem fileSystem = Create(
			"DbLive/Code/sub/c.sql",
			"DbLive/Code/z.sql",
			"DbLive/Code/b.sql",
			"DbLive/Code/a-b/d.sql",
			"DbLive/Code/A.sql"
		);

		// breadth-first, the entries of each folder sorted by name ignoring case.
		Assert.Equal(
			[
				P("Code", "A.sql"),
				P("Code", "b.sql"),
				P("Code", "z.sql"),
				P("Code", "a-b", "d.sql"),
				P("Code", "sub", "c.sql")
			],
			fileSystem.EnumerateFiles(P("Code"), "*.sql", subfolders: true)
		);
	}

	[Fact]
	public void EnumerateFiles_KeepsTheDirectoryAsPassed()
	{
		EmbeddedFileSystem fileSystem = Create("DbLive/Code/v1.sql");

		// Directory.EnumerateFiles on Windows does the same, so relative paths follow the folder names in the settings.
		Assert.Equal([P("code", "v1.sql")], fileSystem.EnumerateFiles(P("code"), "*.sql", subfolders: true));
		Assert.Equal([P("Code", "v1.sql")], fileSystem.EnumerateFiles(P("Code") + "/", "*.sql", subfolders: true));
	}

	[Fact]
	public void EnumerateDirectories_DerivedFromFiles()
	{
		EmbeddedFileSystem fileSystem = Create(
			"DbLive/Tests/t.sql",
			"DbLive/Tests/b/c/t.sql",
			"DbLive/Tests/a/t.sql",
			"DbLive/Code/v.sql"
		);

		Assert.Equal(
			[P("Tests", "a"), P("Tests", "b"), P("Tests", "b", "c")],
			fileSystem.EnumerateDirectories(P("Tests"), "*", SearchOption.AllDirectories)
		);

		Assert.Equal(
			[P("Tests", "a"), P("Tests", "b")],
			fileSystem.EnumerateDirectories(P("Tests"), "*", SearchOption.TopDirectoryOnly)
		);

		Assert.Equal(
			[P("Tests", "b")],
			fileSystem.EnumerateDirectories([P("Tests"), P("Missing")], "b*", SearchOption.AllDirectories)
		);
	}

	[Fact]
	public void PathExists_And_FileExists()
	{
		EmbeddedFileSystem fileSystem = Create("DbLive/Code/dbo/v1.sql");

		Assert.True(fileSystem.PathExists(Root));
		Assert.True(fileSystem.PathExists(P("Code")));
		Assert.True(fileSystem.PathExists(P("code", "DBO")));
		Assert.False(fileSystem.PathExists(P("Co")));
		Assert.False(fileSystem.PathExists(P("Code", "dbo", "v1.sql")));
		Assert.False(fileSystem.PathExists(P("Tests")));

		Assert.True(fileSystem.PathExistsAndNotEmpty(P("Code")));
		Assert.False(fileSystem.IsDirectoryEmpty(P("Code")));

		Assert.True(fileSystem.FileExists(P("Code", "dbo", "v1.sql")));
		Assert.True(fileSystem.FileExists(P("code", "dbo", "V1.SQL")));
		Assert.False(fileSystem.FileExists(P("Code", "dbo")));
		Assert.False(fileSystem.FileExists(P("Code", "dbo", "v2.sql")));
	}

	[Fact]
	public async Task ReadFileDataAsync_RelativePathUsesForwardSlash()
	{
		EmbeddedFileSystem fileSystem = Create("DbLive/Code\\dbo\\v1.sql");

		FileData fileData = await fileSystem.ReadFileDataAsync(P("Code", "dbo", "v1.sql"), Root);

		Assert.Equal("Code/dbo/v1.sql", fileData.RelativePath);
		Assert.Equal(P("Code", "dbo", "v1.sql"), fileData.FilePath);
		Assert.Equal("v1.sql", fileData.FileName);
	}

	[Theory]
	[InlineData("utf-8 with BOM")]
	[InlineData("utf-8")]
	[InlineData("utf-16 with BOM")]
	public async Task ReadFileDataAsync_ReadsTheSameContentAsFileSystem(string encodingName)
	{
		string content = "create or alter view dbo.v1\r\nas\r\nselect N'Grüße' as x;\r\n";
		byte[] bytes = encodingName switch
		{
			"utf-8 with BOM" => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(content)],
			"utf-8" => Encoding.UTF8.GetBytes(content),
			"utf-16 with BOM" => [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(content)],
			_ => throw new ArgumentOutOfRangeException(nameof(encodingName))
		};

		using TempProject project = new();
		string filePath = Path.Combine(project.Path, "Code", "v1.sql");
		_ = Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
		await File.WriteAllBytesAsync(filePath, bytes);

		EmbeddedFileSystem fileSystem = new(Root, ["DbLive/Code/v1.sql"], _ => new MemoryStream(bytes));

		FileData fromFolder = await new FileSystem().ReadFileDataAsync(filePath, project.Path);
		FileData fromAssembly = await fileSystem.ReadFileDataAsync(P("Code", "v1.sql"), Root);

		Assert.Equal(content, fromAssembly.Content);
		Assert.Equal(fromFolder.Content, fromAssembly.Content);
		Assert.Equal(fromFolder.ContentHash, fromAssembly.ContentHash);
		Assert.Equal(fromFolder.RelativePath, fromAssembly.RelativePath);
	}

	[Fact]
	public async Task FileReadAllLinesAsync_SplitsLines()
	{
		EmbeddedFileSystem fileSystem = new(Root, ["DbLive/a.sql"], _ => new MemoryStream(Encoding.UTF8.GetBytes("a\r\nb\nc")));

		Assert.Equal(["a", "b", "c"], await fileSystem.FileReadAllLinesAsync(P("a.sql")));
	}

	[Fact]
	public async Task FileReadAllTextAsync_NotEmbedded_Throws()
	{
		EmbeddedFileSystem fileSystem = Create("DbLive/a.sql");

		_ = await Assert.ThrowsAsync<FileNotFoundException>(() => fileSystem.FileReadAllTextAsync(P("b.sql")));
	}

	[Fact]
	public void FromAssembly_KeepsCultureNamedScriptsInMainAssembly()
	{
		// without WithCulture="false", MSBuild moves v_fr.fr.sql to a French satellite assembly.
		EmbeddedFileSystem fileSystem = EmbeddedFileSystem.FromAssembly(typeof(EmbeddedFileSystemTests).Assembly);

		Assert.Equal("DbLive.Tests.dll", fileSystem.Root);
		Assert.True(fileSystem.FileExists(Path.Combine(fileSystem.Root, "Code", "v_fr.fr.sql")));
	}

	private static string P(params string[] parts) => Path.Combine([Root, .. parts]);

	private static EmbeddedFileSystem Create(params string[] resourceNames) =>
		new(Root, resourceNames, name => new MemoryStream(Encoding.UTF8.GetBytes(name)));
}
