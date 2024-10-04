using EasyFlow.Adapter;

namespace EasyFlow.Deployers.Testing;

internal class UnitTestResultChecker : IUnitTestResultChecker
{
	public CompareResult ValidateTestResult(MultipleResults multiResult)
	{
		if (multiResult.Results.Count > 1)
		{
			if (!multiResult.Results[1].Columns.Any())
			{
				return new CompareResult { Match = true };
			}

			var resultType = multiResult.Results[1].Columns[0];

			if (resultType == "expected")
			{
				var expectedValues = multiResult.Results[1].Rows[0].ColumnValues[0];

				if (expectedValues.ToString() == "rows")
				{
					return CompareResults(multiResult.Results[2], multiResult.Results[0]);
				}
			}
		}
		return new CompareResult { Match = true };
	}

	private CompareResult CompareResults(SqlResult expected, SqlResult actual)
	{
		CompareResult columnsCompareResult = CompareColumns(expected.Columns, actual.Columns);

		if (!columnsCompareResult.Match)
		{
			return new CompareResult
			{
				Match = false,
				Output =
					$"""
					Columns does not match::					
					{columnsCompareResult.Output}
					"""
			};
		}

		for (int i = 0; i < expected.Rows.Count; i++)
		{
			SqlRow expectedRow = expected.Rows[i];
			SqlRow actualRow = actual.Rows[i];

			CompareResult rowCompareResult = CompareColumns(expectedRow.ColumnValues, actualRow.ColumnValues);
			if (!rowCompareResult.Match)
			{
				return new CompareResult
				{
					Match = false,
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

		return new CompareResult { Match = true };
	}

	private CompareResult CompareColumns(List<string> expected, List<string> actual)
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
				if (expected[i] != actual[i])
				{
					match = false;
				}
			}
		}

		if (!match)
		{
			return new CompareResult
			{
				Match = match,
				Output =
					"Expected columns: " + string.Join(", ", expected) + "\n" +
					"Actual columns:   " + string.Join(", ", actual)
			};
		}

		return new CompareResult { Match = match };
	}

	private CompareResult CompareColumns(List<object> expected, List<object> actual)
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
			return new CompareResult
			{
				Match = match,
				Output =
					"Expected values: " + ListToSring(expected) + "\n" +
					"Actual values:   " + ListToSring(actual)
			};
		}

		return new CompareResult { Match = match };
	}

	private string ListToSring(List<object> list)
	{
		return string.Join(", ", list.Select(i => i is string ? $"'{i}'" : i.ToString()));
	}
}
