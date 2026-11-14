namespace DbLive;

/// <summary>
/// A newer DbLive has deployed the database and changed the <c>dblive</c> tables in a way this DbLive cannot read:
/// the minimum DbLive version stored in the database is above the one this DbLive is compatible with.
/// The deployment, or <c>RunAllTestsAsync</c>, stops before it changes anything.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class DatabaseRequiresNewerDbLiveException : Exception
{
	internal DatabaseRequiresNewerDbLiveException(Version requiredVersion, Version minCompatibleVersion, string dbLiveVersion)
		: base(
			$"The database requires DbLive {requiredVersion} or newer: a newer DbLive changed the dblive tables in a way older versions cannot read. "
			+ $"This is DbLive {dbLiveVersion}. Update the DbLive packages to {requiredVersion} or newer. "
			+ "To roll back a release, roll back the application and keep the database (see \"Roll back a release\" in the DbLive docs)."
		)
	{
		RequiredVersion = requiredVersion;
		MinCompatibleVersion = minCompatibleVersion;
	}

	/// <summary>
	/// The minimum DbLive version stored in the database (<c>dblive.version.min_dblive_lib_version</c>).
	/// </summary>
	public Version RequiredVersion { get; }

	/// <summary>
	/// The newest minimum version this DbLive works with: a database that requires this version or an older one deploys.
	/// </summary>
	public Version MinCompatibleVersion { get; }
}
