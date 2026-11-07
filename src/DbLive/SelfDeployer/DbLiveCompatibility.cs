using System.Reflection;

namespace DbLive.SelfDeployer;

/// <summary>
/// Which DbLive can deploy a database. A newer DbLive can store in the <c>dblive</c> tables what an older one cannot read
/// or understands differently, with a self-migration or with its C# code alone. So the database keeps the oldest DbLive
/// version it works with (<c>dblive.version.min_dblive_lib_version</c>), and an older DbLive stops before it changes anything.
/// </summary>
internal static class DbLiveCompatibility
{
	/// <summary>
	/// A database this DbLive has deployed can be deployed by DbLive of this version or newer.
	/// Raise it in the change after which an older DbLive misreads or miswrites the <c>dblive</c> tables,
	/// to the version of the release that ships the change ("Compatibility of the dblive tables" in Development.md).
	/// The release check (<c>.github/scripts/release-notes.cs</c>) reads it from this line.
	/// </summary>
	/// <remarks>
	/// The database is compared with this version, not with the assembly version: the assembly version is
	/// <c>0.0.0-placeholder</c> in a local build and a prerelease on a branch.
	/// </remarks>
	public static readonly Version MinCompatibleVersion = new(2, 0, 0);

	/// <summary>
	/// Fails when the database requires a newer DbLive: its minimum DbLive version is above <see cref="MinCompatibleVersion"/>,
	/// so a DbLive with a change of the <c>dblive</c> tables that this one lacks has deployed it.
	/// </summary>
	/// <param name="minDbLiveLibVersion">The minimum DbLive version of the database; null when it has none.</param>
	/// <exception cref="DatabaseRequiresNewerDbLiveException"/>
	public static void Check(Version? minDbLiveLibVersion)
	{
		if (minDbLiveLibVersion is not null && minDbLiveLibVersion > MinCompatibleVersion)
		{
			throw new DatabaseRequiresNewerDbLiveException(minDbLiveLibVersion, MinCompatibleVersion, GetRunningDbLiveLibVersion());
		}
	}

	/// <summary>
	/// The version of this DbLive for messages: the informational version of the assembly without the commit
	/// (<c>2.1.0</c>, <c>0.0.0-placeholder</c> in a local build).
	/// </summary>
	public static string GetRunningDbLiveLibVersion()
	{
		Assembly assembly = typeof(DbLiveCompatibility).Assembly;
		string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

		return string.IsNullOrEmpty(version)
			? assembly.GetName().Version?.ToString(3) ?? "unknown"
			: version.Split('+')[0];
	}
}
