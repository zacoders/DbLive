using EasyFlow.Adapter;

namespace EasyFlow.Deployers.Testing;

internal class UnitTestResultChecker : IUnitTestResultChecker
{
	public ValidationResult ValidateTestResult(MultipleResults multiResult)
	{
		int? expectedNum = GetExpectedResultPosition(multiResult);

		if (expectedNum is null)
		{
			return new ValidationResult { CompareResult = CompareResult.None };
		}

		// todo: convert to regular class, Expected with properties, so will be easier to access them.
		SqlResult expectedResult = multiResult[expectedNum.Value]; 

		string expectedValue = expectedResult.Rows[0][0].ToString();


		if (expectedValue == "rows") //# todo, add more check types!
		{
			bool columnTypesCheck = expectedResult.GetValue<bool>("type_check", 0);
			for (int i = 0; i < expectedNum.Value; i++)
			{
				ValidationResult compareResult = CompareResults(multiResult[i], multiResult[expectedNum.Value + 1], columnTypesCheck);
				if (compareResult.CompareResult == CompareResult.Mismatch)
				{
					return compareResult;	
				}
			}
			return new ValidationResult { CompareResult = CompareResult.Match };
		}

		throw new Exception($"Not supported expectation {expectedValue}.");
	}

	private int? GetExpectedResultPosition(MultipleResults multiResult)
	{
		if (multiResult.Count(r => r.Columns[0].ColumnName == "expected") > 1)
		{
			new Exception("Just one 'expected' result set is allowed.");
		}

		for (int i = 0; i < multiResult.Count; i++)
		{
			var r = multiResult[i];
			if (r.Columns[0].ColumnName == "expected")
			{
				return i;
			}
		}
		return null;
	}

	private ValidationResult CompareResults(SqlResult expected, SqlResult actual, bool columnTypesCheck)
	{
		ValidationResult columnsCompareResult = CompareColumns(expected.Columns, actual.Columns, columnTypesCheck);
		if (columnsCompareResult.CompareResult == CompareResult.Mismatch) 
			return columnsCompareResult;

		for (int i = 0; i < expected.Rows.Count; i++)
		{
			SqlRow expectedRow = expected.Rows[i];
			SqlRow actualRow = actual.Rows[i];

			ValidationResult rowCompareResult = CompareRows(expectedRow, actualRow);
			if (rowCompareResult.CompareResult == CompareResult.Mismatch)
			{
				return new ValidationResult
				{
					CompareResult = CompareResult.Mismatch,
					Output =
					$"""
					Data for one or more rows does not match:
					Columns: {string.Join(",", expected.Columns)}
					[Row {i + 1}]>
					{rowCompareResult.Output}
					"""
				};
			}
		}

		return new ValidationResult { CompareResult = CompareResult.Match };
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
				CompareResult = CompareResult.Mismatch,
				Output =
					"Columns does not match:" +
					"Expected columns: " + string.Join(", ", expected) + 
					"\n" +
					"Actual columns:   " + string.Join(", ", actual)
			};
		}

		return new ValidationResult { CompareResult = CompareResult.Match };
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
				CompareResult = CompareResult.Mismatch,
				Output =
					"Expected values: " + ListToSring(expected) + "\n" +
					"Actual values:   " + ListToSring(actual)
			};
		}

		return new ValidationResult { CompareResult = CompareResult.Match };
	}

	private string ListToSring(SqlRow row)
	{
		return string.Join(", ", row.Select(i => i is string ? $"'{i}'" : i.ToString()));
	}
}
