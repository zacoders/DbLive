
namespace DbLive.Tests.Testing;

public class UnitTestResultCheckerAssertColumnTests
{
	private static readonly SqlResult ThreeRows = new(
		sqlColumns: [new SqlColumn("Id", "int")],
		resultRows: [new SqlRow(1), new SqlRow(2), new SqlRow(3)]
	);

	[Theory]
	[InlineData("Assert")]
	[InlineData("ASSERT")]
	public void AssertColumn_AnyCase_IsRecognized(string columnName)
	{
		// Arrange
		UnitTestResultChecker checker = new();

		SqlResult assertMark = new(
			sqlColumns: [new SqlColumn(columnName, "...")],
			resultRows: [new SqlRow("row-count=2")]
		);

		// Act
		ValidationResult validationResult = checker.ValidateTestResult([ThreeRows, assertMark]);

		// Assert
		Assert.False(validationResult.IsValid);
		Assert.Equal("Expected 2 row(s), but 3 received.", validationResult.Output);
	}

	[Theory]
	[InlineData("ROW-COUNT=3")]
	[InlineData("row-count = 3")]
	[InlineData(" row-count=3 ")]
	public void AssertValue_CaseAndSpaces_AreIgnored(string assertValue)
	{
		// Arrange
		UnitTestResultChecker checker = new();

		SqlResult assertMark = new(
			sqlColumns: [new SqlColumn("assert", "...")],
			resultRows: [new SqlRow(assertValue)]
		);

		// Act
		ValidationResult validationResult = checker.ValidateTestResult([ThreeRows, assertMark]);

		// Assert
		Assert.True(validationResult.IsValid);
	}

	[Theory]
	[InlineData("asstert", "row-count=3", "is named 'asstert'")]
	[InlineData("asert", "rows", "is named 'asert'")]
	[InlineData("Assret", "has-rows", "is named 'Assret'")]
	[InlineData("", "row-count=3", "has no name")]
	[InlineData("?column?", "rows", "has no name")]
	public void MisnamedAssertColumn_Throws(string columnName, string assertValue, string expectedColumnDescription)
	{
		// Arrange
		UnitTestResultChecker checker = new();

		SqlResult misnamedAssertMark = new(
			sqlColumns: [new SqlColumn(columnName, "...")],
			resultRows: [new SqlRow(assertValue)]
		);

		// Act
		Exception ex = Assert.Throws<Exception>(() => checker.ValidateTestResult([ThreeRows, misnamedAssertMark]));

		// Assert
		Assert.Equal(
			$"A result set with the value '{assertValue}' looks like an assert result set, but its first column {expectedColumnDescription} "
			+ $"instead of 'assert', so the test has no assertion. Rename the column, for example: select '{assertValue}' as assert.",
			ex.Message
		);
	}

	[Theory]
	[InlineData("asstert", "some value")]
	[InlineData("Status", "empty")]
	[InlineData("Name", "rows")]
	public void NoAssertColumn_NotLikeAssert_Valid(string columnName, string value)
	{
		// Arrange
		UnitTestResultChecker checker = new();

		SqlResult result = new(
			sqlColumns: [new SqlColumn(columnName, "nvarchar(20)")],
			resultRows: [new SqlRow(value)]
		);

		// Act
		ValidationResult validationResult = checker.ValidateTestResult([ThreeRows, result]);

		// Assert
		Assert.True(validationResult.IsValid);
	}

	[Fact]
	public void AssertResultSet_NoRows_Throws()
	{
		// Arrange
		UnitTestResultChecker checker = new();

		SqlResult assertMark = new(
			sqlColumns: [new SqlColumn("assert", "...")],
			resultRows: []
		);

		// Act
		Exception ex = Assert.Throws<Exception>(() => checker.ValidateTestResult([ThreeRows, assertMark]));

		// Assert
		Assert.Equal("The 'assert' result set has no rows. Example: select 'rows' as assert.", ex.Message);
	}

	[Theory]
	[InlineData("row-count")]
	[InlineData("row-count=abc")]
	public void AssertValue_RowCountWithoutNumber_Throws(string assertValue)
	{
		// Arrange
		UnitTestResultChecker checker = new();

		SqlResult assertMark = new(
			sqlColumns: [new SqlColumn("assert", "...")],
			resultRows: [new SqlRow(assertValue)]
		);

		// Act
		Exception ex = Assert.Throws<Exception>(() => checker.ValidateTestResult([ThreeRows, assertMark]));

		// Assert
		Assert.Equal("Please use row-count=[Number] to assert row count. Example: row-count=10.", ex.Message);
	}
}
