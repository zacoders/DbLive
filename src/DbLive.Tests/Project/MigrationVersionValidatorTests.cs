namespace DbLive.Tests.Project;

public class MigrationVersionValidatorTests
{
	[Fact]
	public void Validate_Does_Not_Throw_For_Valid_Version_Within_Bounds()
	{
		MockSet mockSet = new();
		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2025, 6, 21));
		MigrationVersionValidator validator = mockSet.CreateUsingMocks<MigrationVersionValidator>();

		validator.Validate("20250621143000.migration.sql");
	}

	[Theory]
	[InlineData("20250621")]
	[InlineData("123")]
	public void Validate_Throws_When_Version_Is_Not_14_Characters(string version)
	{
		MockSet mockSet = new();
		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2025, 6, 21));
		MigrationVersionValidator validator = mockSet.CreateUsingMocks<MigrationVersionValidator>();

		Assert.Throws<InvalidMigrationVersionException>(() =>
			validator.Validate($"{version}.migration.sql"));
	}

	/// <summary>
	/// The version as written counts, not the number: with leading zeros the file name has more than 14 digits.
	/// </summary>
	[Theory]
	[InlineData("Migrations/020250621143000.migration.sql", "020250621143000")]
	[InlineData("Migrations/_Old/00000000000000020250621143000.m.sql", "00000000000000020250621143000")]
	[InlineData("Migrations/001.migration.sql", "001")]
	public void Validate_Throws_For_Leading_Zeros_And_Names_The_Version_As_Written(string relativePath, string version)
	{
		MockSet mockSet = new();
		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2025, 6, 21));
		MigrationVersionValidator validator = mockSet.CreateUsingMocks<MigrationVersionValidator>();

		InvalidMigrationVersionException ex = Assert.Throws<InvalidMigrationVersionException>(() =>
			validator.Validate(relativePath));

		Assert.Contains($"Bad version {version}. Version prefix must be exactly 14 characters long.", ex.Message);
	}

	[Fact]
	public void Validate_Exception_Has_The_Relative_Path_And_The_File_Name()
	{
		MockSet mockSet = new();
		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2025, 6, 21));
		MigrationVersionValidator validator = mockSet.CreateUsingMocks<MigrationVersionValidator>();

		InvalidMigrationVersionException ex = Assert.Throws<InvalidMigrationVersionException>(() =>
			validator.Validate("Migrations/_Old/001.migration.sql"));

		// the same values as the parser gives (MigrationFileNameParserTests).
		Assert.Equal("Migrations/_Old/001.migration.sql", ex.RelativePath);
		Assert.Equal("001.migration.sql", ex.FileName);
		Assert.StartsWith("Invalid migration version in file 'Migrations/_Old/001.migration.sql'. ", ex.Message);
	}

	[Theory]
	[InlineData("20251301120000")]
	[InlineData("20250230010101")]
	public void Validate_Throws_When_Version_Is_Not_A_Valid_Date(string version)
	{
		MockSet mockSet = new();
		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2025, 6, 21));
		MigrationVersionValidator validator = mockSet.CreateUsingMocks<MigrationVersionValidator>();

		Assert.Throws<InvalidMigrationVersionException>(() =>
			validator.Validate($"{version}.migration.sql"));
	}

	[Fact]
	public void Validate_Throws_When_Year_Is_Below_Minimum()
	{
		MockSet mockSet = new();
		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2025, 6, 21));
		MigrationVersionValidator validator = mockSet.CreateUsingMocks<MigrationVersionValidator>();

		Assert.Throws<InvalidMigrationVersionException>(() =>
			validator.Validate("19991231235959.migration.sql"));
	}

	[Fact]
	public void Validate_Throws_When_Year_Is_Above_Maximum()
	{
		MockSet mockSet = new();
		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2025, 1, 1));
		MigrationVersionValidator validator = mockSet.CreateUsingMocks<MigrationVersionValidator>();

		Assert.Throws<InvalidMigrationVersionException>(() =>
			validator.Validate("20260101000000.migration.sql"));
	}

	[Fact]
	public void Validate_Allows_Year_Equal_To_Maximum()
	{
		MockSet mockSet = new();
		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2025, 1, 1));
		MigrationVersionValidator validator = mockSet.CreateUsingMocks<MigrationVersionValidator>();

		validator.Validate("20251231235959.migration.sql");
	}
}
