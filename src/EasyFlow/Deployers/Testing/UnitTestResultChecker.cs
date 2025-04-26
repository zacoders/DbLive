using EasyFlow.Adapter;

namespace EasyFlow.Deployers.Testing;

internal class UnitTestResultChecker : IUnitTestResultChecker
{
	public ValidationResult ValidateTestResult(List<SqlResult> multiResult)
	{
		TestRunSqlResults testRunResults = GetTestRunResults(multiResult);

		if (testRunResults.AssertType == AssertType.None)
		{
			return new ValidationResult { IsValid = true };
		}

		if (testRunResults.AssertType is AssertType.Rows or AssertType.RowsWithSchema)
		{
			for (int i = 0; i < testRunResults.ExpectedResults.Count; i++)
			{
				ValidationResult compareResult = CompareResults(
					testRunResults.ExpectedResults[i],
					testRunResults.ActualResults[i],
					columnTypesCheck: testRunResults.AssertType == AssertType.RowsWithSchema
				);

				if (!compareResult.IsValid)
				{
					return compareResult;
				}
			}
			return new ValidationResult { IsValid = true };
		}


		if (testRunResults.AssertType == AssertType.HasRows)
		{
			if (testRunResults.ActualResults.Count > 0
				&& testRunResults.ActualResults[0].Rows.Count > 0)
			{
				return new ValidationResult { IsValid = true };
			}

			return new ValidationResult
			{
				IsValid = false,
				Output = $"""
				Expected any rows, but empty result set recived.
				"""
			};
		}

		//# todo, add more check types!
		throw new Exception($"Not supported assert {testRunResults.AssertType}.");
	}

	private TestRunSqlResults GetTestRunResults(List<SqlResult> multiResult)
	{
		int? assertResultIndex = GetExpectedResultPosition(multiResult);

		if (!assertResultIndex.HasValue)
		{
			return new TestRunSqlResults
			{
				AssertType = AssertType.None,
				ActualResults = multiResult
			};
		}

		SqlResult expectedResult = multiResult[assertResultIndex.Value];
		string assertTypeStr = expectedResult.GetValue<string>("assert", 0)!;

		TestRunSqlResults testRunResults = new()
		{
			AssertType = assertTypeStr.ToAssertType(),
			ActualResults = multiResult.Take(assertResultIndex.Value).ToList(),
			ExpectedResults = multiResult.Skip(assertResultIndex.Value + 1).ToList()
		};

		return testRunResults;
	}

	private int? GetExpectedResultPosition(List<SqlResult> multiResult)
	{
		if (multiResult.Count(r => r.Columns[0].ColumnName == "assert") > 1)
		{
			new Exception("Just one 'assert' result set is allowed.");
		}

		for (int i = 0; i < multiResult.Count; i++)
		{
			SqlResult r = multiResult[i];
			if (r.Columns[0].ColumnName == "assert")
			{
				return i;
			}
		}
		return null;
	}

	private ValidationResult CompareResults(SqlResult expected, SqlResult actual, bool columnTypesCheck)
	{
		ValidationResult columnsCompareResult = CompareColumns(expected.Columns, actual.Columns, columnTypesCheck);
		if (!columnsCompareResult.IsValid)
			return columnsCompareResult;

		for (int i = 0; i < expected.Rows.Count; i++)
		{
			SqlRow expectedRow = expected.Rows[i];
			SqlRow actualRow = actual.Rows[i];

			ValidationResult rowCompareResult = CompareRows(expectedRow, actualRow);
			if (!rowCompareResult.IsValid)
			{
				return new ValidationResult
				{
					IsValid = false,
					Output = $"""
					Data for one or more rows does not match:
					Columns: {string.Join(",", expected.Columns)}
					[Row {i + 1}]>
					{rowCompareResult.Output}
					"""
				};
			}
		}

		return new ValidationResult { IsValid = true };
	}

	private ValidationResult CompareColumns(List<SqlColumn> expected, List<SqlColumn> actual, bool columnTypesCheck)
	{
		bool match = true;

		if (expected.Count != actual.Count)
		{
			match = false;
		}
		else
		{
			for (int i = 0; i < expected.Count; i++)
			{
				if (columnTypesCheck)
				{
					if (expected[i] != actual[i])
					{
						match = false;
					}
				}
				else
				{
					if (!expected[i].ColumnName.Equals(actual[i].ColumnName, StringComparison.InvariantCulture))
					{
						match = false;
					}
				}
			}
		}

		// TODO: compare columns data types too.

		if (!match)
		{
			return new ValidationResult
			{
				IsValid = false,
				Output = $"""
				Columns does not match:
				Expected columns: {string.Join(", ", expected)}
				Actual columns:   {string.Join(", ", actual)}
				"""
			};
		}

		return new ValidationResult { IsValid = true };
	}

	private ValidationResult CompareRows(SqlRow expected, SqlRow actual)
	{
		bool match = true;
		JsonSerializerSettings settings = new()
		{
			NullValueHandling = NullValueHandling.Include
		};

		for (int i = 0; i < expected.Count; i++)
		{
			if (JsonConvert.SerializeObject(expected[i], settings) != JsonConvert.SerializeObject(actual[i], settings))
			{
				match = false;
			}
		}

		if (!match)
		{
			return new ValidationResult
			{
				IsValid = false,
				Output =
				$"""
				Expected values: {ListToSring(expected)}
				Actual values:   {ListToSring(actual)}
				"""
			};
		}

		return new ValidationResult { IsValid = true };
	}

	private string ListToSring(SqlRow row)
	{
		return string.Join(", ", row.Select(i => i is string ? $"'{i}'" : i.ToString()));
	}
}
