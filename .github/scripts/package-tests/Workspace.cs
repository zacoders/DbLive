using System.Text.RegularExpressions;

/// <summary>
/// The temporary folder of the test projects, outside the repository. MSBuild does not see the Directory.Build.*
/// files of the repository there; NuGet takes the DbLive packages from the local feed into a cache of the folder, so
/// a package rebuilt with the same version is not taken from the global cache; the template is installed into a
/// template cache of the folder too (--debug:custom-hive), not into the one of the user.
/// </summary>
sealed class Workspace(string root, string packages, string version)
{
	public string Root => root;

	/// <summary>The version of the DbLive packages in the local feed.</summary>
	public string Version => version;

	/// <summary>The local feed: the folder with the .nupkg files of one build.</summary>
	public string Packages => packages;

	/// <summary>The template cache of the workspace, for --debug:custom-hive.</summary>
	public string TemplateHive => Path.Combine(root, ".templates");

	public static Workspace Create(string packages)
	{
		string root = Directory.CreateTempSubdirectory("dblive-package-tests-").FullName;
		var workspace = new Workspace(root, packages, DbLiveVersion(packages));

		// Empty files stop MSBuild from looking for these files in the folders above.
		foreach (string name in new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props" })
			workspace.Write(name, "<Project />");

		workspace.Write("nuget.config", $"""
			<?xml version="1.0" encoding="utf-8"?>
			<configuration>
			  <config>
			    <add key="globalPackagesFolder" value="{Path.Combine(root, ".packages")}" />
			  </config>
			  <packageSources>
			    <clear />
			    <add key="local" value="{packages}" />
			    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
			  </packageSources>
			  <packageSourceMapping>
			    <packageSource key="local">
			      <package pattern="DbLive" />
			      <package pattern="DbLive.*" />
			    </packageSource>
			    <packageSource key="nuget.org">
			      <package pattern="*" />
			    </packageSource>
			  </packageSourceMapping>
			</configuration>
			""");

		return workspace;
	}

	/// <summary>
	/// A new project, name/name.csproj, that references these DbLive packages in the version of the feed.
	/// properties and items: more XML for the PropertyGroup and the ItemGroup.
	/// </summary>
	public TestProject Project(string name, string[] packages, string properties = "", string items = "")
	{
		string references = string.Join(Environment.NewLine + "    ",
			packages.Select(id => $"""<PackageReference Include="{id}" Version="{version}" />""").Append(items).Where(item => item.Length > 0));
		Write($"{name}/{name}.csproj", $"""
			<Project Sdk="Microsoft.NET.Sdk">
			  <PropertyGroup>
			    <TargetFramework>net10.0</TargetFramework>
			    {properties}
			  </PropertyGroup>
			  <ItemGroup>
			    {references}
			  </ItemGroup>
			</Project>
			""");
		return new TestProject(this, name);
	}

	public void Write(string relativePath, string content)
	{
		string path = Path.Combine(root, relativePath);
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, content);
	}

	public void Delete() => Directory.Delete(root, recursive: true);

	// DbLive.<version>.nupkg; DbLive.MSSQL.<version>.nupkg does not match, a version starts with a digit.
	static string DbLiveVersion(string packages)
	{
		string[] versions = [.. Directory.GetFiles(packages, "DbLive.*.nupkg")
			.Select(path => Regex.Match(Path.GetFileName(path), @"^DbLive\.(\d.*)\.nupkg$"))
			.Where(m => m.Success)
			.Select(m => m.Groups[1].Value)];
		return versions.Length == 1
			? versions[0]
			: throw new InvalidOperationException($"Expected one DbLive package in {packages}, found {versions.Length}.");
	}
}
