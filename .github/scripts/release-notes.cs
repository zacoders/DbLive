#!/usr/bin/env dotnet
// Release notes from the tasks closed since the previous release ("Releases" in Development.md).
//
//   dotnet run .github/scripts/release-notes.cs                                   preview the notes of the branch
//   dotnet run .github/scripts/release-notes.cs -- --check --title "<PR title>"   what the PR check does
//
// --title <text>    the PR title; without it, the title of the merged PR is read from the HEAD commit
// --version <ver>   the version of the release; without it, the previous tag plus the bump
// --output <file>   write the notes to the file instead of the console
// --check           PR check: the version bump of the PR title and the commits must be the one the tasks need
//
// Fails (exit code 1) when a closed task has no "## Release note", and with --check when the bump differs.
// With --check or --version, also when MinCompatibleVersion changed since the previous release and is not the version
// of this release ("Compatibility of the dblive tables" in Development.md), when a PublicAPI.Unshipped.txt has lines,
// and when a symbol left a PublicAPI.Shipped.txt and the version is not major ("The public API" in Development.md).
// The notes are written in any case, so the PR check can show them next to the errors.
//
// This file is the outline. Each step is a file in release-notes/:
//   ClosedTask.cs      the tasks moved to tasks/done/, their front matter and release note
//   VersionBump.cs     the bump the tasks need, and the bump GitVersion gives for the PR title and the commits
//   Compatibility.cs   MinCompatibleVersion must be the version of the release that changed it
//   PublicApi.cs       the public API files: Unshipped moved to Shipped, a removed symbol only in a major version
//   ReleaseNotes.cs    the Markdown
//   Git.cs, Options.cs
// and public-api/ApiFile.cs of public-api.cs, which reads the public API files.

#:include release-notes/ClosedTask.cs
#:include release-notes/VersionBump.cs
#:include release-notes/Compatibility.cs
#:include release-notes/PublicApi.cs
#:include public-api/ApiFile.cs
#:include release-notes/ReleaseNotes.cs
#:include release-notes/Git.cs
#:include release-notes/Options.cs

using System.Text;

Console.OutputEncoding = Encoding.UTF8;
var options = Options.Parse(args);
Environment.CurrentDirectory = Git.Run("rev-parse", "--show-toplevel").Trim();

// What is released.
string previous = Git.PreviousRelease();
List<ClosedTask> tasks = ClosedTask.AddedSince(previous);

// The version.
VersionBump? needed = VersionBump.NeededBy(tasks);
VersionBump given = VersionBump.GivenBy(options.Title, previous);
bool versionKnown = options.Check || options.Version is not null;
string version = options.Version ?? given.Next(previous);

// The checks.
var errors = new List<string>();
errors.AddRange(ClosedTask.Check(tasks));
if (versionKnown)
{
	errors.AddRange(Compatibility.Check(previous, version));
	errors.AddRange(PublicApi.Check(previous, version));
}
if (options.Check)
	errors.AddRange(VersionBump.Check(needed, given));

// The notes; in the PR check, the version and the errors come first.
var notes = new StringBuilder();
if (options.Check)
	notes.Append(ReleaseNotes.CheckSummary(previous, version, given, errors));
notes.Append(ReleaseNotes.Changes(tasks, options.Title));
notes.Append(ReleaseNotes.ChangelogLink(previous, version));

if (options.Output is null)
	Console.Write(notes);
else
	File.WriteAllText(options.Output, notes.ToString());

foreach (string error in errors)
	Console.Error.WriteLine(Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" ? $"::error::{error}" : $"error: {error}");
return errors.Count == 0 ? 0 : 1;
