namespace DbLive.Deployers.Testing;

internal class UnitTestResultChecker : IUnitTestResultChecker
{
	public ValidationResult ValidateTestResult(List<SqlResult> multiResult)
	{
		TestRunSqlResults testRunResults = GetTestRunResults(multiResult);

		if (testRunResults.AssertInfo.AssertType == AssertType.None)
		{
			return new ValidationResult { IsValid = true };
		}

		if (testRunResults.AssertInfo.AssertType is AssertType.Rows or AssertType.RowsWithSchema)
		{
			return CompareResultSets(
				testRunResults.ExpectedResults,
				testRunResults.ActualResults,
				columnTypesCheck: testRunResults.AssertInfo.AssertType == AssertType.RowsWithSchema
			);
		}


		if (testRunResults.AssertInfo.AssertType == AssertType.HasRows)
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
				Expected any rows, but empty result set received.
				"""
			};
		}


		if (testRunResults.AssertInfo.AssertType == AssertType.RowCount)
		{
			if (testRunResults.ActualResults.Count > 1)
			{
				return new ValidationResult
				{
					IsValid = false,
					Output = $"""
					Only one result set is expected, but multiple received.
					"""
				};
			}

			if (testRunResults.ActualResults.Count == 0)
			{
				return new ValidationResult
				{
					IsValid = false,
					Output = $"""
					Expected {testRunResults.AssertInfo.RowCount} row(s), but no result set received.
					"""
				};
			}

			if (testRunResults.ActualResults[0].Rows.Count == testRunResults.AssertInfo.RowCount)
			{
				return new ValidationResult { IsValid = true };
			}

			return new ValidationResult
			{
				IsValid = false,
				Output = $"""
				Expected {testRunResults.AssertInfo.RowCount} row(s), but {testRunResults.ActualResults[0].Rows.Count} received.
				"""
			};
		}

		throw new Exception($"Not supported assert {testRunResults.AssertInfo.AssertType}.");
	}

	private TestRunSqlResults GetTestRunResults(List<SqlResult> multiResult)
	{
		int? assertResultIndex = UnitTestResultChecker.GetExpectedResultPosition(multiResult);

		if (!assertResultIndex.HasValue)
		{
			ThrowIfLooksLikeMisnamedAssert(multiResult);

			return new TestRunSqlResults
			{
				AssertInfo = new AssertInfo { AssertType = AssertType.None },
				ActualResults = multiResult
			};
		}

		SqlResult expectedResult = multiResult[assertResultIndex.Value];
		if (expectedResult.Rows.Count == 0)
		{
			throw new Exception("The 'assert' result set has no rows. Example: select 'rows' as assert.");
		}
		string assertTypeStr = Convert.ToString(expectedResult.Rows[0][0]) ?? "";

		TestRunSqlResults testRunResults = new()
		{
			AssertInfo = assertTypeStr.ToAssertInfo(),
			ActualResults = multiResult.Take(assertResultIndex.Value).ToList(),
			ExpectedResults = multiResult.Skip(assertResultIndex.Value + 1).ToList()
		};

		return testRunResults;
	}

	private static int? GetExpectedResultPosition(List<SqlResult> multiResult)
	{
		if (multiResult.Count(IsAssertResult) > 1)
		{
			throw new Exception("Just one 'assert' result set is allowed.");
		}

		int index = multiResult.FindIndex(IsAssertResult);
		return index < 0 ? null : index;
	}

	private static bool IsAssertResult(SqlResult result) =>
		result.Columns.Count > 0
		&& result.Columns[0].ColumnName.Equals("assert", StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// Catches a typo in the assert column name (`asstert`) or a missing alias (`select 'rows'`).
	/// Without this check such a test has no assertion and passes silently.
	/// </summary>
	private static void ThrowIfLooksLikeMisnamedAssert(List<SqlResult> multiResult)
	{
		foreach (SqlResult result in multiResult)
		{
			if (result.Columns.Count == 0 || result.Rows.Count == 0) continue;
			if (result.Rows[0][0] is not string value || !value.TryParseAssertInfo(out _)) continue;

			string columnName = result.Columns[0].ColumnName;
			bool unnamed = columnName is "" or "?column?"; // SQL Server and PostgreSQL names of an unnamed column
			if (!unnamed && EditDistance(columnName.ToLowerInvariant(), "assert") > 2) continue;

			string columnDescription = unnamed ? "has no name" : $"is named '{columnName}'";
			throw new Exception(
				$"A result set with the value '{value}' looks like an assert result set, but its first column {columnDescription} "
				+ $"instead of 'assert', so the test has no assertion. Rename the column, for example: select '{value}' as assert."
			);
		}
	}

	private static int EditDistance(string a, string b)
	{
		int[] previous = Enumerable.Range(0, b.Length + 1).ToArray();
		int[] current = new int[b.Length + 1];

		for (int i = 1; i <= a.Length; i++)
		{
			current[0] = i;
			for (int j = 1; j <= b.Length; j++)
			{
				int substitutionCost = a[i - 1] == b[j - 1] ? 0 : 1;
				current[j] = Math.Min(
					Math.Min(previous[j] + 1, current[j - 1] + 1),
					previous[j - 1] + substitutionCost
				);
			}
			(previous, current) = (current, previous);
		}

		return previous[b.Length];
	}

	private ValidationResult CompareResultSets(List<SqlResult> expectedResults, List<SqlResult> actualResults, bool columnTypesCheck)
	{
		if (expectedResults.Count == 0)
		{
			return new ValidationResult
			{
				IsValid = false,
				Output = $"""
				No expected result set after the 'assert' result set.
				"""
			};
		}

		if (expectedResults.Count != actualResults.Count)
		{
			return new ValidationResult
			{
				IsValid = false,
				Output = $"""
				Expected {expectedResults.Count} result set(s), but {actualResults.Count} received.
				"""
			};
		}

		for (int i = 0; i < expectedResults.Count; i++)
		{
			ValidationResult compareResult = CompareResults(expectedResults[i], actualResults[i], columnTypesCheck);

			if (!compareResult.IsValid)
			{
				if (expectedResults.Count == 1)
				{
					return compareResult;
				}

				return new ValidationResult
				{
					IsValid = false,
					Output = $"""
					Result set {i + 1}:
					{compareResult.Output}
					"""
				};
			}
		}

		return new ValidationResult { IsValid = true };
	}

	private ValidationResult CompareResults(SqlResult expected, SqlResult actual, bool columnTypesCheck)
	{
		ValidationResult columnsCompareResult = CompareColumns(expected.Columns, actual.Columns, columnTypesCheck);
		if (!columnsCompareResult.IsValid)
			return columnsCompareResult;

		if (expected.Rows.Count != actual.Rows.Count)
		{
			return new ValidationResult
			{
				IsValid = false,
				Output = $"""
				Expected {expected.Rows.Count} row(s), but {actual.Rows.Count} received.
				"""
			};
		}

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

		var options = new JsonSerializerOptions
		{
			DefaultIgnoreCondition = JsonIgnoreCondition.Never,
			PropertyNamingPolicy = null,
			WriteIndented = false
		};

		for (int i = 0; i < expected.Count; i++)
		{
			var expectedJson = JsonSerializer.Serialize(expected[i], options);
			var actualJson = JsonSerializer.Serialize(actual[i], options);

			if (expectedJson != actualJson)
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
