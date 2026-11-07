using System.Text;

namespace DbLive.Tests.Project;

public class ScriptSourceTests
{
	private const string Location = "MyDb.dll";

	[Fact]
	public void AssemblyScriptSource_RelativePathsUseForwardSlash()
	{
		// %(RecursiveDir) uses '\' when the project is built on Windows and '/' on Linux.
		AssemblyScriptSource scriptSource = CreateAssemblyScriptSource(
			@"DbLive/Migrations\001.migration.sql",
			"DbLive/Code/dbo/v1.sql",
			"DbLive/settings.json"
		);

		Assert.Equal(["Migrations/001.migration.sql", "Code/dbo/v1.sql", "settings.json"], scriptSource.GetFiles());
		Assert.Equal(Location, scriptSource.Location);
	}

	[Fact]
	public void AssemblyScriptSource_IgnoresResourcesWithoutPrefix()
	{
		AssemblyScriptSource scriptSource = CreateAssemblyScriptSource(
			"DbLive/Code/v1.sql",
			"MyDb.Properties.Resources.resources",
			"Other/Code/v2.sql",
			"DbLive.Internal/01.migrations.sql"
		);

		Assert.Equal(["Code/v1.sql"], scriptSource.GetFiles());
	}

	[Fact]
	public async Task AssemblyScriptSource_ReadsResourceByRelativePath()
	{
		AssemblyScriptSource scriptSource = CreateAssemblyScriptSource(@"DbLive/Code\dbo\v1.sql");

		// the content of each test resource is its name.
		Assert.Equal(@"DbLive/Code\dbo\v1.sql", await scriptSource.ReadAsync("Code/dbo/v1.sql"));
	}

	[Fact]
	public async Task AssemblyScriptSource_NotEmbedded_Throws()
	{
		AssemblyScriptSource scriptSource = CreateAssemblyScriptSource("DbLive/a.sql");

		FileNotFoundException ex = await Assert.ThrowsAsync<FileNotFoundException>(() => scriptSource.ReadAsync("b.sql"));

		Assert.Contains("'b.sql'", ex.Message);
		Assert.Contains(Location, ex.Message);
	}

	[Fact]
	public void AssemblyScriptSource_KeepsCultureNamedScriptsInMainAssembly()
	{
		// without WithCulture="false", MSBuild moves v_fr.fr.sql to a French satellite assembly.
		AssemblyScriptSource scriptSource = new(typeof(ScriptSourceTests).Assembly, AssemblyScriptSource.ProjectPrefix);

		Assert.Equal("DbLive.Tests.dll", scriptSource.Location);
		Assert.Contains("Code/v_fr.fr.sql", scriptSource.GetFiles());
	}

	[Fact]
	public void FolderScriptSource_RelativePathsUseForwardSlash()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "")
			.AddFile("Code/dbo/v1.sql", "")
			.AddFile("settings.json", "{}");

		FolderScriptSource scriptSource = new(project.Path);

		Assert.Equal(
			["Code/dbo/v1.sql", "Migrations/001.migration.sql", "settings.json"],
			scriptSource.GetFiles().Order(StringComparer.Ordinal)
		);
		Assert.Equal(project.Path, scriptSource.Location);
	}

	[Theory]
	[InlineData("utf-8 with BOM")]
	[InlineData("utf-8")]
	[InlineData("utf-16 with BOM")]
	public async Task AssemblyScriptSource_ReadsTheSameContentAsFolderScriptSource(string encodingName)
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

		FolderScriptSource folder = new(project.Path);
		AssemblyScriptSource assembly = new(Location, ["DbLive/Code/v1.sql"], _ => new MemoryStream(bytes), AssemblyScriptSource.ProjectPrefix);

		FileData fromFolder = await folder.ReadFileDataAsync(Assert.Single(folder.GetFiles()));
		FileData fromAssembly = await assembly.ReadFileDataAsync(Assert.Single(assembly.GetFiles()));

		Assert.Equal(content, fromAssembly.Content);
		Assert.Equal(fromFolder.Content, fromAssembly.Content);
		Assert.Equal(fromFolder.ContentHash, fromAssembly.ContentHash);
		Assert.Equal(fromFolder.RelativePath, fromAssembly.RelativePath);
	}

	private static AssemblyScriptSource CreateAssemblyScriptSource(params string[] resourceNames) =>
		new(Location, resourceNames, name => new MemoryStream(Encoding.UTF8.GetBytes(name)), AssemblyScriptSource.ProjectPrefix);
}
