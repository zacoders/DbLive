
using NpgsqlTypes;
using System.Data.SqlTypes;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;

namespace DbLive.Tests.Testing;

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
			Expected values: "some different value"
			Actual values:   "some string value"
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

	[Fact]
	public void Rows_SameRowCount_Match()
	{
		// Arrange
		UnitTestResultChecker checker = new();

		SqlResult mainResult = new(
			sqlColumns: [new SqlColumn("Id", "int")],
			resultRows: [new SqlRow(1), new SqlRow(2), new SqlRow(3)]
		);

		SqlResult expectedResult = new(
			sqlColumns: [new SqlColumn("Id", "int")],
			resultRows: [new SqlRow(1), new SqlRow(2), new SqlRow(3)]
		);

		// Act
		ValidationResult validationResult = checker.ValidateTestResult([mainResult, RowsAssertMark(), expectedResult]);

		// Assert
		Assert.True(validationResult.IsValid);
	}

	[Fact]
	public void Rows_MoreActualRows_Fail()
	{
		// Arrange
		UnitTestResultChecker checker = new();

		SqlResult mainResult = new(
			sqlColumns: [new SqlColumn("Id", "int")],
			resultRows: [new SqlRow(1), new SqlRow(2), new SqlRow(3)]
		);

		SqlResult expectedResult = new(
			sqlColumns: [new SqlColumn("Id", "int")],
			resultRows: [new SqlRow(1)]
		);

		// Act
		ValidationResult validationResult = checker.ValidateTestResult([mainResult, RowsAssertMark(), expectedResult]);

		// Assert
		Assert.False(validationResult.IsValid);
		Assert.Equal(
			"""
			Expected 1 row(s), but 3 received.
			"""
			, validationResult.Output
		);
	}

	[Fact]
	public void Rows_FewerActualRows_Fail()
	{
		// Arrange
		UnitTestResultChecker checker = new();

		SqlResult mainResult = new(
			sqlColumns: [new SqlColumn("Id", "int")],
			resultRows: [new SqlRow(1)]
		);

		SqlResult expectedResult = new(
			sqlColumns: [new SqlColumn("Id", "int")],
			resultRows: [new SqlRow(1), new SqlRow(2)]
		);

		// Act
		ValidationResult validationResult = checker.ValidateTestResult([mainResult, RowsAssertMark(), expectedResult]);

		// Assert
		Assert.False(validationResult.IsValid);
		Assert.Equal(
			"""
			Expected 2 row(s), but 1 received.
			"""
			, validationResult.Output
		);
	}

	[Fact]
	public void Rows_MoreActualResultSets_Fail()
	{
		// Arrange
		UnitTestResultChecker checker = new();

		SqlResult mainResult1 = new(
			sqlColumns: [new SqlColumn("Id", "int")],
			resultRows: [new SqlRow(1)]
		);
		SqlResult mainResult2 = new(
			sqlColumns: [new SqlColumn("Name", "nvarchar(10)")],
			resultRows: [new SqlRow("extra")]
		);

		SqlResult expectedResult = new(
			sqlColumns: [new SqlColumn("Id", "int")],
			resultRows: [new SqlRow(1)]
		);

		// Act
		ValidationResult validationResult = checker.ValidateTestResult(
			[mainResult1, mainResult2, RowsAssertMark(), expectedResult]
		);

		// Assert
		Assert.False(validationResult.IsValid);
		Assert.Equal(
			"""
			Expected 1 result set(s), but 2 received.
			"""
			, validationResult.Output
		);
	}

	[Fact]
	public void Rows_FewerActualResultSets_Fail()
	{
		// Arrange
		UnitTestResultChecker checker = new();

		SqlResult mainResult = new(
			sqlColumns: [new SqlColumn("Id", "int")],
			resultRows: [new SqlRow(1)]
		);

		SqlResult expectedResult1 = new(
			sqlColumns: [new SqlColumn("Id", "int")],
			resultRows: [new SqlRow(1)]
		);
		SqlResult expectedResult2 = new(
			sqlColumns: [new SqlColumn("Name", "nvarchar(10)")],
			resultRows: [new SqlRow("second")]
		);

		// Act
		ValidationResult validationResult = checker.ValidateTestResult(
			[mainResult, RowsAssertMark(), expectedResult1, expectedResult2]
		);

		// Assert
		Assert.False(validationResult.IsValid);
		Assert.Equal(
			"""
			Expected 2 result set(s), but 1 received.
			"""
			, validationResult.Output
		);
	}

	[Fact]
	public void Rows_NoExpectedResultSet_Fail()
	{
		// Arrange
		UnitTestResultChecker checker = new();

		SqlResult mainResult = new(
			sqlColumns: [new SqlColumn("Id", "int")],
			resultRows: [new SqlRow(1)]
		);

		// Act
		ValidationResult validationResult = checker.ValidateTestResult([mainResult, RowsAssertMark()]);

		// Assert
		Assert.False(validationResult.IsValid);
		Assert.Equal(
			"""
			No expected result set after the 'assert' result set.
			"""
			, validationResult.Output
		);
	}

	[Fact]
	public void Rows_MultipleResultSets_SecondDoesNotMatch_Fail()
	{
		// Arrange
		UnitTestResultChecker checker = new();

		SqlResult mainResult1 = new(
			sqlColumns: [new SqlColumn("Id", "int")],
			resultRows: [new SqlRow(1)]
		);
		SqlResult mainResult2 = new(
			sqlColumns: [new SqlColumn("Name", "nvarchar(10)")],
			resultRows: [new SqlRow("a"), new SqlRow("b")]
		);

		SqlResult expectedResult1 = new(
			sqlColumns: [new SqlColumn("Id", "int")],
			resultRows: [new SqlRow(1)]
		);
		SqlResult expectedResult2 = new(
			sqlColumns: [new SqlColumn("Name", "nvarchar(10)")],
			resultRows: [new SqlRow("a")]
		);

		// Act
		ValidationResult validationResult = checker.ValidateTestResult(
			[mainResult1, mainResult2, RowsAssertMark(), expectedResult1, expectedResult2]
		);

		// Assert
		Assert.False(validationResult.IsValid);
		Assert.Equal(
			"""
			Result set 2:
			Expected 1 row(s), but 2 received.
			"""
			, validationResult.Output
		);
	}

	[Theory]
	[InlineData(double.NaN)]
	[InlineData(double.PositiveInfinity)]
	[InlineData(double.NegativeInfinity)]
	public void Rows_NaN_and_infinity_equal_themselves(double value)
	{
		UnitTestResultChecker checker = new();

		SqlResult mainResult = new(
			sqlColumns: [new SqlColumn("V", "double precision"), new SqlColumn("F", "real"), new SqlColumn("A", "double precision[]")],
			resultRows: [new SqlRow(value, (float)value, new[] { value, 1 })]
		);

		SqlResult expectedResult = new(
			sqlColumns: [new SqlColumn("V", "double precision"), new SqlColumn("F", "real"), new SqlColumn("A", "double precision[]")],
			resultRows: [new SqlRow(value, (float)value, new[] { value, 1 })]
		);

		ValidationResult validationResult = checker.ValidateTestResult([mainResult, RowsAssertMark(), expectedResult]);

		Assert.True(validationResult.IsValid, validationResult.Output);
	}

	[Theory]
	[InlineData(double.NaN, double.PositiveInfinity)]
	[InlineData(double.PositiveInfinity, double.NegativeInfinity)]
	[InlineData(double.NaN, 0d)]
	public void Rows_NaN_and_infinity_differ_from_other_values(double actual, double expected)
	{
		UnitTestResultChecker checker = new();

		SqlResult mainResult = new(
			sqlColumns: [new SqlColumn("V", "double precision")],
			resultRows: [new SqlRow(actual)]
		);

		SqlResult expectedResult = new(
			sqlColumns: [new SqlColumn("V", "double precision")],
			resultRows: [new SqlRow(expected)]
		);

		ValidationResult validationResult = checker.ValidateTestResult([mainResult, RowsAssertMark(), expectedResult]);

		Assert.False(validationResult.IsValid);
	}

	/// <summary>
	/// Values of every kind that a data reader returns. By default System.Text.Json writes NULL (DBNull), an empty dictionary and a type
	/// without public properties as {}, writes every hierarchyid as {"IsNull":false}, and throws on an IPAddress.
	/// </summary>
	private static readonly Dictionary<string, object> ValuesOfEveryKind = new()
	{
		["int"] = 0,
		["empty string"] = "",
		["string null"] = "null",
		["string {}"] = "{}",
		["bool"] = false,
		["decimal"] = 150.00m,
		["date"] = new DateTime(2026, 1, 1),
		["guid"] = Guid.Empty,
		["empty bytes"] = Array.Empty<byte>(),
		["empty array"] = Array.Empty<int>(),
		["record"] = new object[] { 1 },
		["empty hstore"] = new Dictionary<string, string?>(),
		["macaddr"] = PhysicalAddress.Parse("00-11-22-33-44-55"),
		["inet"] = IPAddress.Parse("192.168.0.1"),
		["pg_lsn"] = NpgsqlLogSequenceNumber.Parse("16/B374D848"),
		["hierarchyid"] = new FakeHierarchyId("/1/"),
	};

	[Theory]
	[InlineData("int")]
	[InlineData("empty string")]
	[InlineData("string null")]
	[InlineData("string {}")]
	[InlineData("bool")]
	[InlineData("decimal")]
	[InlineData("date")]
	[InlineData("guid")]
	[InlineData("empty bytes")]
	[InlineData("empty array")]
	[InlineData("record")]
	[InlineData("empty hstore")]
	[InlineData("macaddr")]
	[InlineData("inet")]
	[InlineData("pg_lsn")]
	[InlineData("hierarchyid")]
	public void Rows_NULL_differs_from_a_value_of_every_kind(string kind)
	{
		object value = ValuesOfEveryKind[kind];

		Assert.False(CheckValue(actual: DBNull.Value, expected: value).IsValid);
		Assert.False(CheckValue(actual: value, expected: DBNull.Value).IsValid);
	}

	[Fact]
	public void Rows_NULL_equals_NULL()
	{
		ValidationResult validationResult = CheckValue(actual: DBNull.Value, expected: DBNull.Value);

		Assert.True(validationResult.IsValid, validationResult.Output);
	}

	[Fact]
	public void Rows_NULL_inside_a_record_differs_from_an_empty_dictionary()
	{
		Assert.False(CheckValue(actual: new object[] { DBNull.Value }, expected: new object[] { new Dictionary<string, string?>() }).IsValid);
	}

	/// <summary>
	/// Two values of a type whose default JSON does not show the value: the same value twice, and two different values.
	/// </summary>
	private static readonly Dictionary<string, (Func<object> Create, object Other)> ValuesWrittenAsText = new()
	{
		["macaddr"] = (() => PhysicalAddress.Parse("00-11-22-33-44-55"), PhysicalAddress.Parse("00-11-22-33-44-56")),
		["inet"] = (() => IPAddress.Parse("192.168.0.1"), IPAddress.Parse("192.168.0.2")),
		["inet v6"] = (() => IPAddress.Parse("::1"), IPAddress.Parse("::2")),
		["inet[]"] = (() => new[] { IPAddress.Parse("10.0.0.1") }, new[] { IPAddress.Parse("10.0.0.2") }),
		["pg_lsn"] = (() => NpgsqlLogSequenceNumber.Parse("16/B374D848"), NpgsqlLogSequenceNumber.Parse("17/B374D848")),
		["hierarchyid"] = (() => new FakeHierarchyId("/1/"), new FakeHierarchyId("/2/")),
	};

	[Theory]
	[InlineData("macaddr")]
	[InlineData("inet")]
	[InlineData("inet v6")]
	[InlineData("inet[]")]
	[InlineData("pg_lsn")]
	[InlineData("hierarchyid")]
	public void Rows_values_without_a_JSON_shape_are_compared_by_their_text(string kind)
	{
		(Func<object> create, object other) = ValuesWrittenAsText[kind];

		ValidationResult same = CheckValue(actual: create(), expected: create());
		ValidationResult different = CheckValue(actual: create(), expected: other);

		Assert.True(same.IsValid, same.Output);
		Assert.False(different.IsValid);
	}

	[Fact]
	public void Rows_SQL_Server_CLR_type_with_IsNull_equals_NULL()
	{
		ValidationResult validationResult = CheckValue(actual: new FakeHierarchyId(null), expected: DBNull.Value);

		Assert.True(validationResult.IsValid, validationResult.Output);
	}

	[Fact]
	public void Rows_failure_message_prints_the_compared_text()
	{
		UnitTestResultChecker checker = new();
		List<SqlColumn> columns =
		[
			new SqlColumn("N", "int"),
			new SqlColumn("D", "numeric"),
			new SqlColumn("A", "integer[]"),
			new SqlColumn("T", "timestamptz"),
			new SqlColumn("S", "text"),
			new SqlColumn("M", "macaddr")
		];

		SqlResult mainResult = new(
			sqlColumns: columns,
			resultRows: [new SqlRow(1, 150.0m, new[] { 1, 3 }, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), "O'Brien Ж", PhysicalAddress.Parse("00-11-22-33-44-55"))]
		);

		SqlResult expectedResult = new(
			sqlColumns: columns,
			resultRows: [new SqlRow(DBNull.Value, 150.00m, new[] { 1, 2 }, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), "O'Brien Ж", PhysicalAddress.Parse("00-11-22-33-44-55"))]
		);

		CultureInfo culture = CultureInfo.CurrentCulture;
		ValidationResult validationResult;
		try
		{
			// The decimal separator of ru-RU is a comma; the message must not depend on it.
			CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
			validationResult = checker.ValidateTestResult([mainResult, RowsAssertMark(), expectedResult]);
		}
		finally
		{
			CultureInfo.CurrentCulture = culture;
		}

		Assert.False(validationResult.IsValid);
		Assert.Equal(
			"""
			Data for one or more rows does not match:
			Columns: N: int,D: numeric,A: integer[],T: timestamptz,S: text,M: macaddr
			[Row 1]>
			Expected values: null, 150.00, [1,2], "2026-01-01T00:00:00Z", "O'Brien Ж", "001122334455"
			Actual values:   1, 150.0, [1,3], "2026-01-01T00:00:00", "O'Brien Ж", "001122334455"
			"""
			, validationResult.Output
		);
	}

	[Fact]
	public void Rows_value_of_a_type_without_properties_and_without_ToString_fails_the_test()
	{
		ValidationResult validationResult = CheckValue(actual: new NoProperties(), expected: new NoProperties());

		Assert.False(validationResult.IsValid);
		Assert.Equal(
			$"""
			Cannot compare the expected value of the column 'V' in row 1, of the type {typeof(NoProperties)}: The type {typeof(NoProperties)} has no public properties and does not override ToString(), so its values cannot be told apart.
			Select the value as text in the test.
			"""
			, validationResult.Output
		);
	}

	[Fact]
	public void Rows_value_whose_property_throws_fails_the_test()
	{
		ValidationResult validationResult = CheckValue(actual: new ThrowingProperty(), expected: 1);

		Assert.False(validationResult.IsValid);
		Assert.Equal(
			$"""
			Cannot compare the actual value of the column 'V' in row 1, of the type {typeof(ThrowingProperty)}: The property cannot be read.
			Select the value as text in the test.
			"""
			, validationResult.Output
		);
	}

	private static ValidationResult CheckValue(object actual, object expected)
	{
		UnitTestResultChecker checker = new();

		SqlResult mainResult = new(
			sqlColumns: [new SqlColumn("V", "...")],
			resultRows: [new SqlRow(actual)]
		);

		SqlResult expectedResult = new(
			sqlColumns: [new SqlColumn("V", "...")],
			resultRows: [new SqlRow(expected)]
		);

		return checker.ValidateTestResult([mainResult, RowsAssertMark(), expectedResult]);
	}

	/// <summary>
	/// Like SqlHierarchyId of Microsoft.SqlServer.Types: the only public property is IsNull, and the value is in ToString().
	/// </summary>
	private sealed class FakeHierarchyId(string? text) : INullable
	{
		public bool IsNull => text is null;

		public override string ToString() => text ?? "NULL";
	}

	private sealed class NoProperties;

	private sealed class ThrowingProperty
	{
		public int Value => throw new InvalidOperationException("The property cannot be read.");
	}

	private static SqlResult RowsAssertMark() => new(
		sqlColumns: [new SqlColumn("assert", "...")],
		resultRows: [new SqlRow("rows")]
	);
}