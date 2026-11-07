
namespace DbLive.Deployers.Testing;

public static class AssertTypeExtensions
{
	static Dictionary<AssertType, string> assertTypesMap = new()
	{
		{AssertType.Rows, "rows" },
		{AssertType.RowsWithSchema, "rows-with-schema" },
		{AssertType.HasRows, "has-rows" },
		{AssertType.RowCount, "row-count" },
		{AssertType.Empty, "empty" },
		{AssertType.SingleRow, "single-row" }
	};

	static Dictionary<string, AssertType> reverseAssertTypesMap =
		assertTypesMap.ToDictionary(i => i.Value, i => i.Key);

	public static AssertInfo ToAssertInfo(this string assertDefinition)
	{
		if (assertDefinition.TryParseAssertInfo(out AssertInfo? assertInfo))
		{
			return assertInfo;
		}

		if (assertDefinition.Contains("row-count", StringComparison.OrdinalIgnoreCase))
		{
			throw new Exception($"Please use row-count=[Number] to assert row count. Example: row-count=10.");
		}

		throw new Exception($"Unknown assert type {assertDefinition}. Supported values: {SupportedAssertTypes()}");
	}

	public static bool TryParseAssertInfo(this string assertDefinition, [NotNullWhen(true)] out AssertInfo? assertInfo)
	{
		string definition = assertDefinition.Trim().ToLowerInvariant();

		// bare "row-count" has no number, it is parsed below as "row-count=N"
		if (reverseAssertTypesMap.TryGetValue(definition, out AssertType value) && value != AssertType.RowCount)
		{
			assertInfo = value switch
			{
				AssertType.Empty => new AssertInfo { AssertType = AssertType.RowCount, RowCount = 0 },
				AssertType.SingleRow => new AssertInfo { AssertType = AssertType.RowCount, RowCount = 1 },
				_ => new AssertInfo { AssertType = value }
			};
			return true;
		}

		string[] parts = definition.Split('=', 2);
		if (parts.Length == 2
			&& parts[0].Trim() == "row-count"
			&& int.TryParse(parts[1], out int rowCount))
		{
			assertInfo = new AssertInfo { AssertType = AssertType.RowCount, RowCount = rowCount };
			return true;
		}

		assertInfo = null;
		return false;
	}

	private static string SupportedAssertTypes() =>
		string.Join(", ", reverseAssertTypesMap.Keys);
}