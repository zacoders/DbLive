using System.IO.Compression;
using System.Xml.Linq;

/// <summary>The .nupkg files of the feed themselves, before a project uses them.</summary>
static class Packages
{
	/// <summary>
	/// Every DLL under lib/ of a package has its documentation file next to it, the .xml that GenerateDocumentationFile
	/// writes from the doc comments (task 0096-94). IntelliSense of a project that references the package reads it from there.
	/// A package without a DLL, the template, is skipped.
	/// </summary>
	public static void ExpectDocumentation(Workspace workspace)
	{
		foreach (string path in Directory.GetFiles(workspace.Packages, "*.nupkg").Order(StringComparer.Ordinal))
		{
			using ZipArchive package = ZipFile.OpenRead(path);
			string[] dlls = [.. package.Entries
				.Select(e => e.FullName)
				.Where(n => n.StartsWith("lib/", StringComparison.Ordinal) && n.EndsWith(".dll", StringComparison.Ordinal))
				.Order(StringComparer.Ordinal)];
			if (dlls.Length == 0)
				continue;

			Report.Section(Path.GetFileName(path));
			foreach (string dll in dlls)
				CheckDocumentation(package, dll);
		}
	}

	static void CheckDocumentation(ZipArchive package, string dll)
	{
		string xml = Path.ChangeExtension(dll, ".xml");
		ZipArchiveEntry? entry = package.GetEntry(xml);
		if (entry is null)
		{
			Report.Fail($"{dll} has no {xml}.");
			return;
		}

		using Stream stream = entry.Open();
		XElement root = XDocument.Load(stream).Root!;
		string? assembly = root.Element("assembly")?.Element("name")?.Value;
		int members = root.Element("members")?.Elements("member").Count() ?? 0;
		if (assembly != Path.GetFileNameWithoutExtension(dll))
			Report.Fail($"{xml} documents the assembly '{assembly}'.");
		else if (members == 0)
			Report.Fail($"{xml} documents no member.");
		else
			Report.Ok($"{xml}, {members} documented member(s)");
	}
}
