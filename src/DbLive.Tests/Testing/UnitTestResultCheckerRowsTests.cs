using DbLive.Adapter;
using DbLive.Deployers.Testing;

namespace EasyFlow.Tests.Testing;

public class UnitTestResultCheckerRowsTests
{
	[Fact]
	public void Rows_Simple_Success()
	{
		// Arrange		
		UnitTestResultChecker checker = new();

		SqlResult mainResult = new(
			sqlColumns: [new SqlColumn("Test", "nvarchar(128)")],
			resultRows: [new SqlRow("some string value")]
		);

		SqlResult assertMark = new(
			sqlColumns: [new SqlColumn("assert", "...")],
			resultRows: [new SqlRow("rows")]
		);

		SqlResult expectedResult = new(
			sqlColumns: [new SqlColumn("Test", "nvarchar(30)")],
			resultRows: [new SqlRow("some string value")]
		);


		// Act
		ValidationResult validationResult = checker.ValidateTestResult([mainResult, assertMark, expectedResult]);

		// Assert
		Assert.True(validationResult.IsValid);
	}

	[Fact]
	public void RowsWithSchema_Columns_DoesNotMatch_Fail()
	{
		// Arrange		
		UnitTestResultChecker checker = new();

		SqlResult mainResult = new(
			sqlColumns: [new SqlColumn("Test", "nvarchar(128)")],
			resultRows: [new SqlRow("some string value")]
		);

		SqlResult assertMark = new(
			sqlColumns: [new SqlColumn("assert", "..."),],
			resultRows: [new SqlRow("rows-with-schema")]
		);

		SqlResult expectedResult = new(
			sqlColumns: [new SqlColumn("Test", "nvarchar(30)")],
			resultRows: [new SqlRow("some string value")]
		);


		// Act
		ValidationResult validationResult = checker.ValidateTestResult([mainResult, assertMark, expectedResult]);

		// Assert
		Assert.False(validationResult.IsValid);
		Assert.Equal(
			"""
			Columns does not match:
			Expected columns: Test: nvarchar(30)
			Actual columns:   Test: nvarchar(128)
			"""
			, validationResult.Output
		);
	}

	[Fact]
	public void Rows_DoesNotMatch_Values_Fail()
	{
		// Arrange		
		UnitTestResultChecker checker = new();

		SqlResult mainResult = new(
			sqlColumns: [new SqlColumn("Test", "nvarchar(128)")],
			resultRows: [new SqlRow("some string value")]
		);

		SqlResult assertMark = new(
			sqlColumns: [new SqlColumn("assert", "...")],
			resultRows: [new SqlRow("rows")]
		);

		SqlResult expectedResult = new(
			sqlColumns: [new SqlColumn("Test", "nvarchar(30)")],
			resultRows: [new SqlRow("some different value")]
		);


		// Act
		ValidationResult validationResult = checker.ValidateTestResult([mainResult, assertMark, expectedResult]);

		// Assert
		Assert.False(validationResult.IsValid);
		Assert.Equal(
			"""
			Data for one or more rows does not match:
			Columns: Test: nvarchar(30)
			[Row 1]>
			Expected values: 'some different value'
			Actual values:   'some string value'
			"""
			, validationResult.Output
		);
	}


	[Fact]
	public void Rows_MultipleResultSets_Match()
	{
		// Arrange		
		UnitTestResultChecker checker = new();

		SqlResult mainResult1 = new(
			sqlColumns: [new SqlColumn("Test", "nvarchar(128)")],
			resultRows: [new SqlRow("some string value")]
		);
		SqlResult mainResult2 = new(
			sqlColumns: [new SqlColumn("Test2", "nvarchar(255)")],
			resultRows: [new SqlRow("some test value 2")]
		);

		SqlResult assertMark = new(
			sqlColumns: [new SqlColumn("assert", "...")],
			resultRows: [new SqlRow("rows")]
		);

		SqlResult expectedResult1 = new(
			sqlColumns: [new SqlColumn("Test", "nvarchar(30)")],
			resultRows: [new SqlRow("some string value")]
		);
		SqlResult expectedResult2 = new(
			sqlColumns: [new SqlColumn("Test2", "nvarchar(30)")],
			resultRows: [new SqlRow("some test value 2")]
		);

		// Act
		ValidationResult validationResult = checker.ValidateTestResult(
			[mainResult1, mainResult2, assertMark, expectedResult1, expectedResult2]
		);

		// Assert
		Assert.True(validationResult.IsValid);
	}
}