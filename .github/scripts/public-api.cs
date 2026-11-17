#!/usr/bin/env dotnet
// Moves the public API of a branch to PublicAPI.Shipped.txt ("The public API" in Development.md).
//
//   dotnet run .github/scripts/public-api.cs
//
// For every package with a C# API (src/<project>/PublicAPI.Shipped.txt): the lines of PublicAPI.Unshipped.txt go to
// PublicAPI.Shipped.txt, where the code fix of RS0016 would sort them, and a '*REMOVED*' line deletes its symbol.
// PublicAPI.Unshipped.txt keeps only its header. A pull request runs it before the merge: the "Release notes" check
// fails while a PublicAPI.Unshipped.txt has lines.
//
// This file is the outline. The step is public-api/ApiFile.cs, which release-notes.cs includes too.

#:include public-api/ApiFile.cs

// the root of the repository: the script may be run from any folder of it.
string? root = Environment.CurrentDirectory;
while (root is not null && !Directory.Exists(Path.Combine(root, ".github")))
	root = Path.GetDirectoryName(root);
Environment.CurrentDirectory = root ?? throw new InvalidOperationException("Run the script inside the DbLive repository.");

int total = 0;
foreach (string package in ApiFile.Packages())
{
	int moved = ApiFile.Move(package);
	if (moved > 0)
		Console.WriteLine($"{package}: {moved} line(s) moved to {ApiFile.Shipped}.");
	total += moved;
}

Console.WriteLine(total == 0 ? $"Nothing to move: every {ApiFile.Unshipped} is empty." : "Done. Review the diff of PublicAPI.Shipped.txt.");
return 0;
