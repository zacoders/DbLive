namespace DbLive.Tests.Project;

public class MigrationVersionValidatorTests
{
	[Fact]
	public void Validate_Does_Not_Throw_For_Valid_Version_Within_Bounds()
	{
		MockSet mockSet = new();
		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2025, 6, 21));
		MigrationVersionValidator validator = mockSet.CreateUsingMocks<MigrationVersionValidator>();

		validator.Validate(20250621143000, "20250621143000.migration.sql");
	}

	[Theory]
	[InlineData(20250621)]
	[InlineData(123)]
	public void Validate_Throws_When_Version_Is_Not_14_Characters(long version)
	{
		MockSet mockSet = new();
		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2025, 6, 21));
		MigrationVersionValidator validator = mockSet.CreateUsingMocks<MigrationVersionValidator>();

		Assert.Throws<InvalidMigrationVersionException>(() =>
			validator.Validate(version, $"{version}.migration.sql"));
	}

	[Fact]
	public void Validate_Exception_Has_The_Relative_Path_And_The_File_Name()
	{
		MockSet mockSet = new();
		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2025, 6, 21));
		MigrationVersionValidator validator = mockSet.CreateUsingMocks<MigrationVersionValidator>();

		InvalidMigrationVersionException ex = Assert.Throws<InvalidMigrationVersionException>(() =>
			validator.Validate(1, "Migrations/_Old/001.migration.sql"));

		// the same values as the parser gives (MigrationFileNameParserTests).
		Assert.Equal("Migrations/_Old/001.migration.sql", ex.RelativePath);
		Assert.Equal("001.migration.sql", ex.FileName);
		Assert.StartsWith("Invalid migration version in file 'Migrations/_Old/001.migration.sql'. ", ex.Message);
	}

	[Theory]
	[InlineData(20251301120000)]
	[InlineData(20250230010101)]
	public void Validate_Throws_When_Version_Is_Not_A_Valid_Date(long version)
	{
		MockSet mockSet = new();
		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2025, 6, 21));
		MigrationVersionValidator validator = mockSet.CreateUsingMocks<MigrationVersionValidator>();

		Assert.Throws<InvalidMigrationVersionException>(() =>
			validator.Validate(version, $"{version}.migration.sql"));
	}

	[Fact]
	public void Validate_Throws_When_Year_Is_Below_Minimum()
	{
		MockSet mockSet = new();
		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2025, 6, 21));
		MigrationVersionValidator validator = mockSet.CreateUsingMocks<MigrationVersionValidator>();

		Assert.Throws<InvalidMigrationVersionException>(() =>
			validator.Validate(19991231235959, "19991231235959.migration.sql"));
	}

	[Fact]
	public void Validate_Throws_When_Year_Is_Above_Maximum()
	{
		MockSet mockSet = new();
		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2025, 1, 1));
		MigrationVersionValidator validator = mockSet.CreateUsingMocks<MigrationVersionValidator>();

		Assert.Throws<InvalidMigrationVersionException>(() =>
			validator.Validate(20260101000000, "20260101000000.migration.sql"));
	}

	[Fact]
	public void Validate_Allows_Year_Equal_To_Maximum()
	{
		MockSet mockSet = new();
		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2025, 1, 1));
		MigrationVersionValidator validator = mockSet.CreateUsingMocks<MigrationVersionValidator>();

		validator.Validate(20251231235959, "20251231235959.migration.sql");
	}
}
