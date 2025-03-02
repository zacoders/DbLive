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
			sqlColumns: [new SqlColumn("expected", "nvarchar(128)")],
			resultRows: [new SqlRow("rows")]
		);

		SqlResult expectedResult = new(
			sqlColumns: [new SqlColumn("Test", "nvarchar(128)")],
			resultRows: [new SqlRow("some string value")]
		);


		// Act
		ValidationResult validationResult = checker.ValidateTestResult([mainResult, expectedMark, expectedResult]);

		// Assert
		Assert.Equal(CompareResult.Match, validationResult.CompareResult);
	}

}