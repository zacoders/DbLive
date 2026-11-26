using System.Reflection;

namespace DbLive.Tests;

/// <summary>
/// The files of <c>Microsoft.CodeAnalysis.PublicApiAnalyzers</c> next to the .csproj of each package with a C# API
/// ("The public API" in Development.md).
/// </summary>
public class PublicApiFilesTests
{
	private const string Shipped = "PublicAPI.Shipped.txt";
	private const string Unshipped = "PublicAPI.Unshipped.txt";

	/// <summary>
	/// Every PublicAPI.Unshipped.txt is empty, also in a branch: a public symbol goes to PublicAPI.Shipped.txt in the commit
	/// that adds it, and a removed symbol is a line deleted from PublicAPI.Shipped.txt.
	/// </summary>
	/// <remarks>
	/// Why the API is never kept in PublicAPI.Unshipped.txt:
	/// <list type="bullet">
	/// <item>
	/// Every merge to main is a release ("Releases" in Development.md). The analyzer's split into shipped and unshipped API
	/// is for projects that merge for a while and release later; here nothing is on main and not shipped. So the
	/// PublicAPI.Shipped.txt of a branch is the API that the branch releases, and an unshipped state has no meaning.
	/// </item>
	/// <item>
	/// A task is closed in one commit together with its code, docs and task file ("Same commit" in AGENTS.md). Its public
	/// API belongs to that commit as well: the diff of PublicAPI.Shipped.txt in the commit is the API change of the task,
	/// next to its release note. A line left in PublicAPI.Unshipped.txt reaches PublicAPI.Shipped.txt later, in the commit of
	/// another task or in a commit of its own, and the history no longer shows which task changed the API.
	/// </item>
	/// <item>
	/// A changed or removed symbol is plain to see: its line is changed or deleted in PublicAPI.Shipped.txt, the way the
	/// "Release notes" check finds a breaking change (a line of the previous release that is gone). Through
	/// PublicAPI.Unshipped.txt it would be a <c>*REMOVED*</c> line in another file, and the diff of
	/// PublicAPI.Shipped.txt would not show it until the move.
	/// </item>
	/// <item>
	/// A branch closes many tasks before its pull request. The "Release notes" check fails on lines in
	/// PublicAPI.Unshipped.txt too, but it runs on the pull request, after all of them. This test fails at <c>dotnet test</c>,
	/// in the commit that leaves the line there.
	/// </item>
	/// </list>
	/// The code fix of RS0016 in the IDE, and <c>dotnet format analyzers</c>, write to PublicAPI.Unshipped.txt. After them run
	/// <c>dotnet run .github/scripts/public-api.cs</c>: it moves the lines to PublicAPI.Shipped.txt, sorted as the code fix
	/// sorts them, and applies the <c>*REMOVED*</c> lines.
	/// </remarks>
	[Fact]
	public void Unshipped_files_are_empty()
	{
		string srcDir = typeof(PublicApiFilesTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "SrcDir").Value!;

		// the packages with a C# API: the project folders of src/ that have PublicAPI.Shipped.txt, as public-api.cs finds them.
		string[] packages = [.. Directory.GetDirectories(srcDir).Where(folder => File.Exists(Path.Combine(folder, Shipped))).Order(StringComparer.Ordinal)];

		Assert.NotEmpty(packages);

		string[] unshippedLines =
		[
			.. packages.SelectMany(package =>
			{
				string path = Path.Combine(package, Unshipped);

				return File.Exists(path)
					? File.ReadAllLines(path)
						.Where(line => line.Trim().Length > 0 && !line.StartsWith('#'))
						.Select(line => $"{Path.GetFileName(package)}/{Unshipped}: {line}")
					: [];
			})
		];

		if (unshippedLines.Length > 0)
		{
			Assert.Fail(
				$"PublicAPI.Unshipped.txt must be empty: put the public API straight into {Shipped} (see the remarks of this test). "
				+ "Run 'dotnet run .github/scripts/public-api.cs' to move the lines. Lines:"
				+ Environment.NewLine
				+ string.Join(Environment.NewLine, unshippedLines)
			);
		}
	}
}
