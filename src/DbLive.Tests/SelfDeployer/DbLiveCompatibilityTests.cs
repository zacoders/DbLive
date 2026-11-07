namespace DbLive.Tests.SelfDeployer;

public class DbLiveCompatibilityTests
{
	private static readonly Version _min = DbLiveCompatibility.MinCompatibleVersion;

	[Fact]
	public void MinCompatibleVersion_is_a_release_version()
	{
		// the release check compares it with the version of the release, and the database stores it as major.minor.patch.
		Assert.Matches(@"^\d+\.\d+\.\d+$", DbLiveCompatibility.MinCompatibleVersion.ToString());
	}

	public static TheoryData<Version?> AllowedVersions => [
		null,
		new Version(0, 0, 1),
		new Version(_min.Major, _min.Minor, _min.Build),
	];

	[Theory]
	[MemberData(nameof(AllowedVersions))]
	public void Check_passes_without_a_minimum_or_with_one_up_to_MinCompatibleVersion(Version? minDbLiveLibVersion)
	{
		DbLiveCompatibility.Check(minDbLiveLibVersion);
	}

	public static TheoryData<Version> NewerVersions => [
		new Version(_min.Major, _min.Minor, _min.Build + 1),
		new Version(_min.Major, _min.Minor + 1, 0),
		new Version(_min.Major + 1, 0, 0),
	];

	[Theory]
	[MemberData(nameof(NewerVersions))]
	public void Check_fails_when_the_minimum_is_above_MinCompatibleVersion(Version minDbLiveLibVersion)
	{
		DatabaseRequiresNewerDbLiveException ex = Assert.Throws<DatabaseRequiresNewerDbLiveException>(
			() => DbLiveCompatibility.Check(minDbLiveLibVersion));

		Assert.Equal(minDbLiveLibVersion, ex.RequiredVersion);
		Assert.Equal(DbLiveCompatibility.MinCompatibleVersion, ex.MinCompatibleVersion);
		Assert.Contains($" This is DbLive {DbLiveCompatibility.GetRunningDbLiveLibVersion()}. ", ex.Message);
	}

	[Fact]
	public void DbLive_version_is_the_informational_version_without_the_commit()
	{
		string version = DbLiveCompatibility.GetRunningDbLiveLibVersion();

		Assert.NotEmpty(version);
		Assert.DoesNotContain('+', version);
	}

	[Fact]
	public void Exception_message_names_the_required_version_and_this_version()
	{
		DatabaseRequiresNewerDbLiveException ex = new(new Version(2, 3, 0), new Version(2, 0, 0), "2.1.0");

		Assert.Equal(
			"The database requires DbLive 2.3.0 or newer: a newer DbLive changed the dblive tables in a way older versions cannot read. "
			+ "This is DbLive 2.1.0. Update the DbLive packages to 2.3.0 or newer. "
			+ "To roll back a release, roll back the application and keep the database (see \"Roll back a release\" in the DbLive docs).",
			ex.Message
		);
	}
}
