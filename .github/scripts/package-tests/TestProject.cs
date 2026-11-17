using System.Reflection;
using System.Runtime.Loader;

/// <summary>A project in the workspace: its files, its build, and what the DbLive packages add to the built assembly.</summary>
sealed class TestProject(Workspace workspace, string name)
{
	string Dir => Path.Combine(workspace.Root, name);

	string OutputDir => Path.Combine(Dir, "bin", "Debug", "net10.0");

	public TestProject Write(string relativePath, string content)
	{
		workspace.Write(Path.Combine(name, relativePath), content);
		return this;
	}

	/// <summary>
	/// The build embeds these scripts (paths without the DbLive/ prefix) and writes the ProjectDir and
	/// DbLiveScriptsFolder metadata.
	/// </summary>
	public void ExpectScripts(string scriptsFolder, string[] resources) => ExpectBuild(resources, scriptsFolder);

	/// <summary>The build embeds no scripts and writes no metadata.</summary>
	public void ExpectNoScripts() => ExpectBuild(resources: [], scriptsFolder: null);

	/// <summary>The build fails with this message.</summary>
	public void ExpectBuildError(string error)
	{
		Report.Section(name);
		var (exitCode, output) = Build();
		if (exitCode == 0)
			Report.Fail("the build passed, expected it to fail.");
		else if (!output.Contains(error, StringComparison.Ordinal))
			Report.Fail($"the build failed without '{error}':{Environment.NewLine}{output}");
		else
			Report.Ok($"the build failed with '{error}'");
	}

	void ExpectBuild(string[] resources, string? scriptsFolder)
	{
		Report.Section(name);
		var (exitCode, output) = Build();
		if (exitCode != 0)
		{
			Report.Fail($"the build failed (exit code {exitCode}):{Environment.NewLine}{output}");
			return;
		}

		// Loaded from bytes in a context of its own: the file is not locked, and the metadata is read without
		// creating the attributes.
		var context = new AssemblyLoadContext(name, isCollectible: true);
		try
		{
			Assembly assembly = context.LoadFromStream(new MemoryStream(File.ReadAllBytes(Path.Combine(OutputDir, $"{name}.dll"))));
			CheckResources(assembly, resources);
			CheckMetadata(assembly, scriptsFolder);
		}
		finally
		{
			context.Unload();
		}

		// name.fr.sql must stay in the assembly, not go to a satellite assembly in fr/.
		if (Directory.Exists(Path.Combine(OutputDir, "fr")))
			Report.Fail("a satellite assembly folder fr/ was created.");
	}

	(int ExitCode, string Output) Build() => Dotnet.Run("build", Dir, "-nologo", "-v:q");

	static void CheckResources(Assembly assembly, string[] resources)
	{
		string[] actual = [.. assembly.GetManifestResourceNames()
			.Where(r => r.StartsWith("DbLive/", StringComparison.Ordinal))
			.Select(r => r["DbLive/".Length..].Replace('\\', '/'))
			.Order(StringComparer.Ordinal)];
		string[] expected = [.. resources.Order(StringComparer.Ordinal)];
		if (!actual.SequenceEqual(expected))
			Report.Fail($"resources DbLive/[{string.Join(", ", actual)}], expected DbLive/[{string.Join(", ", expected)}].");
		else
			Report.Ok($"{actual.Length} script(s) embedded as DbLive/...");
	}

	// The DbLiveScriptsFolder metadata comes with the ProjectDir metadata; scriptsFolder null: neither.
	void CheckMetadata(Assembly assembly, string? scriptsFolder)
	{
		var metadata = assembly.GetCustomAttributesData()
			.Where(a => a.AttributeType.FullName == "System.Reflection.AssemblyMetadataAttribute")
			.ToDictionary(a => (string)a.ConstructorArguments[0].Value!, a => (string?)a.ConstructorArguments[1].Value);
		metadata.TryGetValue("ProjectDir", out string? actualProjectDir);
		metadata.TryGetValue("DbLiveScriptsFolder", out string? actualScriptsFolder);
		string? expectedProjectDir = scriptsFolder is null ? null : Dir + Path.DirectorySeparatorChar;
		if (!string.Equals(actualProjectDir, expectedProjectDir, StringComparison.OrdinalIgnoreCase)
			|| actualScriptsFolder != scriptsFolder)
			Report.Fail($"metadata ProjectDir '{actualProjectDir}', DbLiveScriptsFolder '{actualScriptsFolder}'; expected '{expectedProjectDir}', '{scriptsFolder}'.");
		else
			Report.Ok(scriptsFolder is null ? "no ProjectDir metadata" : "ProjectDir and DbLiveScriptsFolder metadata");
	}
}
