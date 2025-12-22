using DbLive.Adapter;
using DbLive.Deployers.Testing;

namespace DbLive.Tests.Testing;

public class UnitTestResultCheckerHasRowsTests
{
	[Fact]
	public void HasRows_Valid()
	{
		// Arrange		
		UnitTestResultChecker checker = new();

		SqlResult mainResult = new(
			sqlColumns: [new SqlColumn("Test", "nvarchar(128)")],
			resultRows: [new SqlRow("some string value")]
		);

		SqlResult assertMark = new(
			sqlColumns: [new SqlColumn("assert", "..."),],
			resultRows: [new SqlRow("has-rows")]
		);

		SqlResult expectedResult = new(
			sqlColumns: [new SqlColumn("SomeOtherColumn", "nvarchar(30)")],
			resultRows: [new SqlRow("some different value")]
		);


		// Act
		ValidationResult validationResult = checker.ValidateTestResult([mainResult, assertMark, expectedResult]);

		// Assert
		Assert.True(validationResult.IsValid);
	}


	[Fact]
	public void HasRows_NoActualRowsReturned_Fail()
	{
		// Arrange		
		UnitTestResultChecker checker = new();

		SqlResult actualResult = new(
			sqlColumns: [new SqlColumn("Test", "nvarchar(128)")],
			resultRows: []
		);

		SqlResult assertMark = new(
			sqlColumns: [new SqlColumn("assert", "..."),],
			resultRows: [new SqlRow("has-rows")]
		);

		SqlResult expectedResult = new(
			sqlColumns: [new SqlColumn("SomeOtherColumn", "nvarchar(30)")],
			resultRows: [new SqlRow("some string value")]
		);

		// Act
		ValidationResult validationResult = checker.ValidateTestResult([actualResult, assertMark, expectedResult]);

		// Assert
		Assert.False(validationResult.IsValid);
	}


	[Fact]
	public void HasRows_NoActualResult_Fail()
	{
		// Arrange		
		UnitTestResultChecker checker = new();

		SqlResult assertMark = new(
			sqlColumns: [new SqlColumn("assert", "..."),],
			resultRows: [new SqlRow("has-rows")]
		);

		SqlResult expectedResult = new(
			sqlColumns: [new SqlColumn("SomeOtherColumn", "nvarchar(30)")],
			resultRows: [new SqlRow("some string value")]
		);

		// Act
		ValidationResult validationResult = checker.ValidateTestResult([assertMark, expectedResult]);

		// Assert
		Assert.False(validationResult.IsValid);
	}

}