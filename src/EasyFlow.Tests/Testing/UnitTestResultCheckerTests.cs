using EasyFlow.Adapter;
using EasyFlow.Deployers.Testing;

namespace EasyFlow.Tests.Testing;

public class UnitTestResultCheckerTests
{
	[Fact]
	public void Simple_Rows_Match()
	{
		// Arrange		
		UnitTestResultChecker checker = new();

		SqlResult mainResult = new(
			sqlColumns: [new SqlColumn("Test", "nvarchar(128)")],
			resultRows: [new SqlRow("some string value")]
		);

		SqlResult expectedMark = new(
			sqlColumns: [new SqlColumn("assert", "...")],
			resultRows: [new SqlRow("rows")]
		);

		SqlResult expectedResult = new(
			sqlColumns: [new SqlColumn("Test", "nvarchar(30)")],
			resultRows: [new SqlRow("some string value")]
		);


		// Act
		ValidationResult validationResult = checker.ValidateTestResult([mainResult, expectedMark, expectedResult]);

		// Assert
		Assert.True(validationResult.IsValid);
	}

	[Fact]
	public void Columns_DoesNotMatch_SchemaTypeCheck()
	{
		// Arrange		
		UnitTestResultChecker checker = new();

		SqlResult mainResult = new(
			sqlColumns: [new SqlColumn("Test", "nvarchar(128)")],
			resultRows: [new SqlRow("some string value")]
		);

		SqlResult expectedMark = new(
			sqlColumns: [new SqlColumn("assert", "..."),],
			resultRows: [new SqlRow("rows-with-schema")]
		);

		SqlResult expectedResult = new(
			sqlColumns: [new SqlColumn("Test", "nvarchar(30)")],
			resultRows: [new SqlRow("some string value")]
		);


		// Act
		ValidationResult validationResult = checker.ValidateTestResult([mainResult, expectedMark, expectedResult]);

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
	public void Rows_DoesNotMatch()
	{
		// Arrange		
		UnitTestResultChecker checker = new();

		SqlResult mainResult = new(
			sqlColumns: [new SqlColumn("Test", "nvarchar(128)")],
			resultRows: [new SqlRow("some string value")]
		);

		SqlResult expectedMark = new(
			sqlColumns: [new SqlColumn("assert", "...")],
			resultRows: [new SqlRow("rows")]
		);

		SqlResult expectedResult = new(
			sqlColumns: [new SqlColumn("Test", "nvarchar(30)")],
			resultRows: [new SqlRow("some different value")]
		);


		// Act
		ValidationResult validationResult = checker.ValidateTestResult([mainResult, expectedMark, expectedResult]);

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
	public void Simple_MultipleResults_Match()
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

		SqlResult expectedMark = new(
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
			[mainResult1, mainResult2, expectedMark, expectedResult1, expectedResult2]
		);

		// Assert
		Assert.True(validationResult.IsValid);
	}
}