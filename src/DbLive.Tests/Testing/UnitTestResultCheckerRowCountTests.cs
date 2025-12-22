using DbLive.Adapter;
using DbLive.Deployers.Testing;

namespace DbLive.Tests.Testing;

public class UnitTestResultCheckerRowCountTests
{
	[Fact]
	public void RowCount_Valid()
	{
		// Arrange		
		UnitTestResultChecker checker = new();

		SqlResult mainResult = new(
			sqlColumns: [new SqlColumn("Test", "nvarchar(128)")],
			resultRows: [
				new SqlRow("row1"),
				new SqlRow("row2"),
				new SqlRow("row3")
			]
		);

		SqlResult assertMark = new(
			sqlColumns: [new SqlColumn("assert", "..."),],
			resultRows: [new SqlRow("row-count=3")]
		);

		// Act
		ValidationResult validationResult = checker.ValidateTestResult([mainResult, assertMark]);

		// Assert
		Assert.True(validationResult.IsValid);
	}

	[Fact]
	public void RowCount_Invalid()
	{
		// Arrange		
		UnitTestResultChecker checker = new();

		SqlResult mainResult = new(
			sqlColumns: [new SqlColumn("Test", "nvarchar(128)")],
			resultRows: [
				new SqlRow("row1"),
				new SqlRow("row2"),
				new SqlRow("row3")
			]
		);

		SqlResult assertMark = new(
			sqlColumns: [new SqlColumn("assert", "..."),],
			resultRows: [new SqlRow("row-count=2")]
		);

		// Act
		ValidationResult validationResult = checker.ValidateTestResult([mainResult, assertMark]);

		// Assert
		Assert.False(validationResult.IsValid);
		Assert.Equal(
		"""
		Expected 2 row(s), but 3 received.
		""", validationResult.Output);
	}


	[Fact]
	public void SingleRow_Invalid()
	{
		// Arrange		
		UnitTestResultChecker checker = new();

		SqlResult mainResult = new(
			sqlColumns: [new SqlColumn("Test", "nvarchar(128)")],
			resultRows: [
				new SqlRow("row1"),
				new SqlRow("row2")
			]
		);

		SqlResult assertMark = new(
			sqlColumns: [new SqlColumn("assert", "..."),],
			resultRows: [new SqlRow("single-row")]
		);

		// Act
		ValidationResult validationResult = checker.ValidateTestResult([mainResult, assertMark]);

		// Assert
		Assert.False(validationResult.IsValid);
		Assert.Equal(
		"""
		Expected 1 row(s), but 2 received.
		""", validationResult.Output);
	}


	[Fact]
	public void SingleRow_Valid()
	{
		// Arrange		
		UnitTestResultChecker checker = new();

		SqlResult mainResult = new(
			sqlColumns: [new SqlColumn("Test", "nvarchar(128)")],
			resultRows: [
				new SqlRow("row1")
			]
		);

		SqlResult assertMark = new(
			sqlColumns: [new SqlColumn("assert", "..."),],
			resultRows: [new SqlRow("single-row")]
		);

		// Act
		ValidationResult validationResult = checker.ValidateTestResult([mainResult, assertMark]);

		// Assert
		Assert.True(validationResult.IsValid);
	}

	[Fact]
	public void RowCount_Zero_Valid()
	{
		// Arrange		
		UnitTestResultChecker checker = new();

		SqlResult mainResult = new(
			sqlColumns: [new SqlColumn("Test", "nvarchar(128)")],
			resultRows: []
		);

		SqlResult assertMark = new(
			sqlColumns: [new SqlColumn("assert", "..."),],
			resultRows: [new SqlRow("row-count=0")]
		);

		// Act
		ValidationResult validationResult = checker.ValidateTestResult([mainResult, assertMark]);

		// Assert
		Assert.True(validationResult.IsValid);
	}


	[Fact]
	public void RowCount_Zero_NoActualResult_Valid()
	{
		// Arrange		
		UnitTestResultChecker checker = new();

		SqlResult assertMark = new(
			sqlColumns: [new SqlColumn("assert", "..."),],
			resultRows: [new SqlRow("row-count=0")]
		);

		// Act
		ValidationResult validationResult = checker.ValidateTestResult([assertMark]);

		// Assert
		Assert.False(validationResult.IsValid);
		Assert.Equal(
		"""
		Expected 0 rows, but result set is not recieved.
		""", validationResult.Output);
	}


	[Fact]
	public void Empty_ZeroReceived_Valid()
	{
		// Arrange		
		UnitTestResultChecker checker = new();

		SqlResult mainResult = new(
			sqlColumns: [new SqlColumn("Test", "nvarchar(128)")],
			resultRows: []
		);

		SqlResult assertMark = new(
			sqlColumns: [new SqlColumn("assert", "..."),],
			resultRows: [new SqlRow("empty")]
		);

		// Act
		ValidationResult validationResult = checker.ValidateTestResult([mainResult, assertMark]);

		// Assert
		Assert.True(validationResult.IsValid);
	}


	[Fact]
	public void Empty_Zero_NoActualResult_Valid()
	{
		// Arrange		
		UnitTestResultChecker checker = new();

		SqlResult assertMark = new(
			sqlColumns: [new SqlColumn("assert", "..."),],
			resultRows: [new SqlRow("empty")]
		);

		// Act
		ValidationResult validationResult = checker.ValidateTestResult([assertMark]);

		// Assert
		Assert.False(validationResult.IsValid);
		Assert.Equal(
		"""
		Expected 0 rows, but result set is not recieved.
		""", validationResult.Output);
	}
}